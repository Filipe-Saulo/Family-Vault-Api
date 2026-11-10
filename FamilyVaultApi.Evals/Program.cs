using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using FamilyVaultApi.Evals;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// Uso:
//   dotnet run --project FamilyVaultApi.Evals                                (só valida o dataset; não chama a API)
//   dotnet run --project FamilyVaultApi.Evals -- --estimate                  (conta tokens; a contagem não é cobrada)
//   dotnet run --project FamilyVaultApi.Evals -- --run [--models claude-haiku-5-5,claude-opus-5-5] [--effort low] [--repeat 1] [--cases luz-pdf,agua-pdf] [--out pasta]
//   dotnet run --project FamilyVaultApi.Evals -- --cli [mesmas opções]        (pelo Claude Code logado: assinatura, sem chave e sem cobrança por uso)
// A chave (só --run e --estimate) vem de Ai:ApiKey (user-secrets da API, ou a variável Ai__ApiKey) ou de ANTHROPIC_API_KEY.

var options = EvalOptions.Parse(args);
var dataset = Dataset.Load(Path.Combine(AppContext.BaseDirectory, "Dataset"));
if (options.Cases != null)
    dataset = dataset.Only(options.Cases);
Console.WriteLine($"Dataset válido: {dataset.Cases.Count} casos ({string.Join(", ", dataset.Cases.GroupBy(c => c.Kind).Select(g => $"{g.Count()} {g.Key}"))}).");
if (!options.Run && !options.EstimateOnly && !options.Cli)
{
    Console.WriteLine("Nada chamado. Use --cli para rodar pelo Claude Code (assinatura), --estimate para contar tokens ou --run para rodar pela API (custa dinheiro).");
    return 0;
}

if (options.Cli)
{
    // Amostra pelo Claude Code: mesmo prompt, schema e mapeamento; o relatório marca o modelo com "-cli" e sai sem custo
    Console.WriteLine($"Pelo Claude Code. Modelos: {string.Join(", ", options.Models)}; esforço: {options.Effort ?? "padrão do modelo"}; repetições: {options.Repeat}");
    Directory.CreateDirectory(options.OutputDirectory);
    var cliSummaries = new List<ModelSummary>();
    foreach (var model in options.Models)
    {
        var results = new List<CaseResult>();
        for (var round = 1; round <= options.Repeat; round++)
        {
            foreach (var evalCase in dataset.Cases)
            {
                var watch = Stopwatch.StartNew();
                var outcome = await ClaudeCodeRunner.RunAsync(model, options.Effort, evalCase, dataset);
                watch.Stop();

                var result = Scoring.Score(evalCase, outcome.Extraction, outcome.Failure, outcome.Error);
                result.Round = round;
                result.InputTokens = outcome.InputTokens;
                result.OutputTokens = outcome.OutputTokens;
                result.Seconds = watch.Elapsed.TotalSeconds;
                results.Add(result);
                Console.WriteLine($"  [{model}] {evalCase.Id,-28} {(result.Passed ? "ok " : "ERRO")} {result.Detail}");
            }
        }

        var summary = ModelSummary.From(model + "-cli", options.Effort, results);
        cliSummaries.Add(summary);
        await Report.WriteModelAsync(options.OutputDirectory, summary, results);
    }

    await Report.WriteComparisonAsync(options.OutputDirectory, cliSummaries);
    Console.WriteLine($"Relatórios em {Path.GetFullPath(options.OutputDirectory)}");
    return 0;
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(EvalOptions).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();
var apiKey = configuration["Ai:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
    apiKey = configuration["ANTHROPIC_API_KEY"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Sem chave: defina Ai:ApiKey (user-secrets ou Ai__ApiKey) ou ANTHROPIC_API_KEY.");
    return 2;
}

Console.WriteLine($"Modelos: {string.Join(", ", options.Models)}; esforço: {options.Effort ?? "padrão do modelo"}; repetições: {options.Repeat}");

var usage = new UsageRecorder();
var client = new AnthropicClient
{
    ApiKey = apiKey,
    MaxRetries = 3,
    Timeout = TimeSpan.FromSeconds(180),
    HttpClient = new HttpClient(usage)
};

if (options.EstimateOnly)
{
    await Estimate.RunAsync(client, dataset, options);
    return 0;
}

Directory.CreateDirectory(options.OutputDirectory);
var summaries = new List<ModelSummary>();

foreach (var model in options.Models)
{
    var aiOptions = new AiOptions { Model = model, Effort = options.Effort, MaxTokens = 16000 };
    var extractor = new AnthropicChargeExtractor(client, Options.Create(aiOptions));
    var results = new List<CaseResult>();

    for (var round = 1; round <= options.Repeat; round++)
    {
        foreach (var evalCase in dataset.Cases)
        {
            usage.Reset();
            var watch = Stopwatch.StartNew();
            ChargeExtractionResult? extraction = null;
            string? failure = null;
            string? error = null;
            try
            {
                extraction = await extractor.ExtractAsync(dataset.InputFor(evalCase), CancellationToken.None);
            }
            catch (ChargeExtractionException ex)
            {
                failure = ex.Message;
            }
            catch (Exception ex)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
            }
            watch.Stop();

            var result = Scoring.Score(evalCase, extraction, failure, error);
            result.Round = round;
            result.InputTokens = usage.InputTokens;
            result.OutputTokens = usage.OutputTokens;
            result.Seconds = watch.Elapsed.TotalSeconds;
            results.Add(result);

            Console.WriteLine($"  [{model}] {evalCase.Id,-28} {(result.Passed ? "ok " : "ERRO")} {result.Detail}");
        }
    }

    var summary = ModelSummary.From(model, options.Effort, results);
    summaries.Add(summary);
    await Report.WriteModelAsync(options.OutputDirectory, summary, results);
}

await Report.WriteComparisonAsync(options.OutputDirectory, summaries);
Console.WriteLine($"Relatórios em {Path.GetFullPath(options.OutputDirectory)}");
return 0;

namespace FamilyVaultApi.Evals
{
    public class EvalOptions
    {
        public static readonly string[] DefaultModels = ["claude-haiku-5-5", "claude-sonnet-5-5", "claude-opus-5-5"];

        public List<string> Models { get; private set; } = [.. DefaultModels];
        public string? Effort { get; private set; }
        public int Repeat { get; private set; } = 1;
        public bool EstimateOnly { get; private set; }
        public bool Run { get; private set; }
        public bool Cli { get; private set; }
        public List<string>? Cases { get; private set; }
        public string OutputDirectory { get; private set; } =
            Path.Combine("eval-reports", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

        public static EvalOptions Parse(string[] args)
        {
            var options = new EvalOptions();
            for (var i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Falta o valor de {args[i]}.");
                switch (args[i])
                {
                    case "--models":
                        options.Models = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        break;
                    case "--effort":
                        var effort = Next();
                        options.Effort = string.IsNullOrWhiteSpace(effort) ? null : effort;
                        break;
                    case "--repeat":
                        options.Repeat = Math.Max(1, int.Parse(Next(), CultureInfo.InvariantCulture));
                        break;
                    case "--out":
                        options.OutputDirectory = Next();
                        break;
                    case "--run":
                        options.Run = true;
                        break;
                    case "--cases":
                        options.Cases = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                        break;
                    case "--cli":
                        options.Cli = true;
                        break;
                    case "--estimate":
                        options.EstimateOnly = true;
                        break;
                    default:
                        throw new ArgumentException($"Argumento desconhecido: {args[i]}.");
                }
            }
            return options;
        }
    }

    // Preço por 1M de tokens (entrada, saída), em dólares. Modelo fora da lista sai sem custo no relatório.
    public static class Pricing
    {
        private static readonly Dictionary<string, (decimal Input, decimal Output)> PerMillion = new()
        {
            ["claude-haiku-5-5"] = (0.10m, 0.50m),
            ["claude-sonnet-5-5"] = (2m, 10m),
            ["claude-opus-5-5"] = (4m, 20m),
            ["claude-fable-5-2"] = (8m, 40m)
        };

        public static decimal? Cost(string model, long inputTokens, long outputTokens) =>
            PerMillion.TryGetValue(model, out var price)
                ? (inputTokens * price.Input + outputTokens * price.Output) / 1_000_000m
                : null;
    }

    // Soma o "usage" das respostas da API (inclui as novas tentativas do SDK)
    public class UsageRecorder : DelegatingHandler
    {
        public UsageRecorder() : base(new HttpClientHandler()) { }

        public long InputTokens { get; private set; }
        public long OutputTokens { get; private set; }

        public void Reset()
        {
            InputTokens = 0;
            OutputTokens = 0;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "application/json")
                return response;

            await response.Content.LoadIntoBufferAsync();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("usage", out var used))
            {
                InputTokens += Sum(used, "input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens");
                OutputTokens += Sum(used, "output_tokens");
            }
            return response;
        }

        private static long Sum(JsonElement usage, params string[] names) =>
            names.Sum(n => usage.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0);
    }

    public static class Estimate
    {
        // Saída estimada por documento (JSON + raciocínio adaptativo); a contagem real sai no relatório
        private const int OutputTokensPerCaseLow = 300;
        private const int OutputTokensPerCaseHigh = 1500;

        public static async Task RunAsync(AnthropicClient client, Dataset dataset, EvalOptions options)
        {
            long inputTokens = 0;
            var extractor = new AnthropicChargeExtractor(client, Options.Create(new AiOptions { Model = options.Models[0] }));
            foreach (var evalCase in dataset.Cases)
            {
                var parameters = extractor.BuildParams(dataset.InputFor(evalCase));
                var count = await client.Messages.CountTokens(new MessageCountTokensParams
                {
                    Model = options.Models[0],
                    System = AnthropicChargeExtractor.SystemPrompt,
                    Messages = [.. parameters.Messages],
                    OutputConfig = parameters.OutputConfig
                });
                inputTokens += count.InputTokens;
                Console.WriteLine($"  {evalCase.Id,-28} {count.InputTokens,6} tokens de entrada");
            }

            Console.WriteLine();
            Console.WriteLine($"Entrada por rodada (todos os casos): {inputTokens:N0} tokens (contados em {options.Models[0]}; o tokenizer é o mesmo nos modelos 5.x)");
            decimal totalLow = 0, totalHigh = 0;
            foreach (var model in options.Models)
            {
                var low = Pricing.Cost(model, inputTokens, OutputTokensPerCaseLow * dataset.Cases.Count) * options.Repeat;
                var high = Pricing.Cost(model, inputTokens, OutputTokensPerCaseHigh * dataset.Cases.Count) * options.Repeat;
                totalLow += low ?? 0;
                totalHigh += high ?? 0;
                Console.WriteLine($"  {model,-20} US$ {Money(low)} a US$ {Money(high)}");
            }
            Console.WriteLine($"  {"total",-20} US$ {totalLow:0.00} a US$ {totalHigh:0.00} ({options.Repeat} repetição(ões))");
        }

        private static string Money(decimal? value) => value.HasValue ? value.Value.ToString("0.000", CultureInfo.InvariantCulture) : "?";
    }
}
