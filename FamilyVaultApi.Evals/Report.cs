using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FamilyVaultApi.Evals
{
    public class ModelSummary
    {
        public string Model { get; init; } = "";
        public string? Effort { get; init; }
        public int Runs { get; init; }
        public double PassRate { get; init; }
        public Dictionary<string, double?> FieldAccuracy { get; init; } = new();
        public double? NotChargeAccuracy { get; init; }
        public double? IllegibleDetected { get; init; }
        public double? NoInventedData { get; init; }
        public double? InjectionPassRate { get; init; }
        public int Errors { get; init; }
        public long InputTokens { get; init; }
        public long OutputTokens { get; init; }
        public decimal? CostUsd { get; init; }
        public double MeanSeconds { get; init; }
        public double MaxSeconds { get; init; }

        public static ModelSummary From(string model, string? effort, List<CaseResult> results)
        {
            static double? Rate(IEnumerable<bool?> values)
            {
                var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                return list.Count == 0 ? null : list.Count(v => v) / (double)list.Count;
            }

            var charges = results.Where(r => r.AmountOk.HasValue).ToList();
            var input = results.Sum(r => r.InputTokens);
            var output = results.Sum(r => r.OutputTokens);

            return new ModelSummary
            {
                Model = model,
                Effort = effort,
                Runs = results.Count,
                PassRate = results.Count(r => r.Passed) / (double)results.Count,
                FieldAccuracy = new Dictionary<string, double?>
                {
                    ["isCharge"] = Rate(charges.Select(r => r.IsChargeOk)),
                    ["emissor"] = Rate(charges.Select(r => r.IssuerOk)),
                    ["valor"] = Rate(charges.Select(r => r.AmountOk)),
                    ["vencimento"] = Rate(charges.Select(r => r.DueDateOk)),
                    ["linha digitável"] = Rate(charges.Select(r => r.BarcodeOk)),
                    ["categoria"] = Rate(charges.Select(r => r.CategoryOk))
                },
                NotChargeAccuracy = Rate(results.Where(r => r.Kind == "not_charge").Select(r => (bool?)r.Passed)),
                IllegibleDetected = Rate(results.Select(r => r.IllegibleDetected)),
                NoInventedData = Rate(results.Select(r => r.NoInventedData)),
                InjectionPassRate = Rate(results.Where(r => r.Kind == "injection").Select(r => (bool?)r.Passed)),
                Errors = results.Count(r => r.Error != null),
                InputTokens = input,
                OutputTokens = output,
                CostUsd = Pricing.Cost(model, input, output),
                MeanSeconds = results.Average(r => r.Seconds),
                MaxSeconds = results.Max(r => r.Seconds)
            };
        }
    }

    public static class Report
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public static async Task WriteModelAsync(string directory, ModelSummary summary, List<CaseResult> results)
        {
            var name = FileName(summary);
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".json"),
                JsonSerializer.Serialize(new { summary, results }, Json));

            var md = new StringBuilder();
            md.AppendLine($"# Evals do extrator: {summary.Model}");
            md.AppendLine();
            md.AppendLine($"Esforço: {summary.Effort ?? "padrão do modelo"} · execuções: {summary.Runs} · custo: {Money(summary.CostUsd)} · " +
                          $"tokens: {summary.InputTokens:N0} entrada, {summary.OutputTokens:N0} saída · tempo médio {summary.MeanSeconds:0.0} s (máx. {summary.MaxSeconds:0.0} s)");
            md.AppendLine();
            AppendSummaryTable(md, [summary]);
            md.AppendLine();
            md.AppendLine("## Casos");
            md.AppendLine();
            md.AppendLine("| Caso | Tipo | Rodada | Resultado | Detalhe |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var r in results)
                md.AppendLine($"| {r.CaseId} | {r.Kind} | {r.Round} | {(r.Passed ? "ok" : "**erro**")} | {r.Detail.Replace("|", "/")} |");

            await File.WriteAllTextAsync(Path.Combine(directory, name + ".md"), md.ToString());
        }

        public static async Task WriteComparisonAsync(string directory, List<ModelSummary> summaries)
        {
            var md = new StringBuilder();
            md.AppendLine("# Evals do extrator: comparação entre modelos");
            md.AppendLine();
            md.AppendLine($"Gerado em {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. Um arquivo por modelo, com os casos, ao lado deste.");
            md.AppendLine();
            AppendSummaryTable(md, summaries);
            md.AppendLine();
            md.AppendLine("Campos: acerto nos casos em que o esperado é uma conta (contas e prompt injection). " +
                          "Não-conta: documentos que não são cobrança classificados como tal. Ilegível: devolveu a falha \"ilegível\"; " +
                          "sem invenção: não devolveu valor nem linha digitável para o ilegível. Injeção: todos os campos certos apesar do texto malicioso.");

            await File.WriteAllTextAsync(Path.Combine(directory, "comparacao.md"), md.ToString());
            await File.WriteAllTextAsync(Path.Combine(directory, "comparacao.json"), JsonSerializer.Serialize(summaries, Json));

            // No GitHub Actions, a tabela também vai para o resumo do job
            var stepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (!string.IsNullOrEmpty(stepSummary))
                await File.AppendAllTextAsync(stepSummary, md.ToString());
        }

        private static void AppendSummaryTable(StringBuilder md, List<ModelSummary> summaries)
        {
            var fields = summaries[0].FieldAccuracy.Keys.ToList();
            md.AppendLine("| Modelo | Esforço | Casos ok | " + string.Join(" | ", fields) +
                          " | Não-conta | Ilegível | Sem invenção | Injeção | Erros | Custo | Tempo médio |");
            md.AppendLine("|---|---|---|" + string.Concat(fields.Select(_ => "---|")) + "---|---|---|---|---|---|---|");
            foreach (var s in summaries)
                md.AppendLine($"| {s.Model} | {s.Effort ?? "padrão"} | {Pct(s.PassRate)} | " +
                              string.Join(" | ", fields.Select(f => Pct(s.FieldAccuracy[f]))) +
                              $" | {Pct(s.NotChargeAccuracy)} | {Pct(s.IllegibleDetected)} | {Pct(s.NoInventedData)} | {Pct(s.InjectionPassRate)}" +
                              $" | {s.Errors} | {Money(s.CostUsd)} | {s.MeanSeconds:0.0} s |");
        }

        private static string FileName(ModelSummary s) => s.Effort == null ? s.Model : $"{s.Model}-{s.Effort}";

        private static string Pct(double? value) =>
            value.HasValue ? (value.Value * 100).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";

        private static string Money(decimal? value) =>
            value.HasValue ? "US$ " + value.Value.ToString("0.000", CultureInfo.InvariantCulture) : "?";
    }
}
