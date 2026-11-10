using System.Diagnostics;
using System.Text.Json;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.Service;

namespace FamilyVaultApi.Evals
{
    // Roda um caso pelo Claude Code em modo não interativo (claude -p), com a assinatura de quem está logado:
    // sem chave de API e sem cobrança por uso. Mesmo prompt de sistema, mesmo schema e mesmo mapeamento do
    // extrator real; o que muda é o transporte (o modelo lê o arquivo pela ferramenta Read do Claude Code).
    // É uma amostra: não exercita o SDK nem a requisição que a API faz em produção.
    public static class ClaudeCodeRunner
    {
        public record Outcome(ChargeExtractionResult? Extraction, string? Failure, string? Error, long InputTokens, long OutputTokens);

        public static async Task<Outcome> RunAsync(string model, string? effort, EvalCase evalCase, Dataset dataset)
        {
            // Pasta temporária só com o documento: o Read fica restrito a ela e nada da máquina entra no contexto
            var workDir = Path.Combine(Path.GetTempPath(), "family-vault-evals", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            var fileName = "documento" + Path.GetExtension(evalCase.File);
            File.Copy(Path.Combine(dataset.FilesDirectory, evalCase.File), Path.Combine(workDir, fileName));

            try
            {
                var input = dataset.InputFor(evalCase);
                var prompt = $"O documento está no arquivo {fileName}, nesta pasta. Leia-o com a ferramenta Read.\n\n" +
                             AnthropicChargeExtractor.BuildCategoryText(input.ExpenseCategories);

                var start = new ProcessStartInfo("claude")
                {
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                foreach (var arg in new[]
                {
                    "-p", prompt,
                    "--model", model,
                    "--system-prompt", AnthropicChargeExtractor.SystemPrompt,
                    "--json-schema", AnthropicChargeExtractor.SchemaJson,
                    "--output-format", "json",
                    // Só leitura, só nesta pasta, sem MCP, sem as configurações e o CLAUDE.md de quem roda. Sem
                    // --allowedTools: pré-aprovar o Read liberaria caminhos fora da pasta; sem ele, o modo não interativo
                    // nega o que pediria permissão
                    "--tools", "Read",
                    "--restricted",
                    "--strict-mcp-config",
                    "--setting-sources", "",
                    "--no-session-persistence"
                })
                    start.ArgumentList.Add(arg);
                if (!string.IsNullOrWhiteSpace(effort))
                {
                    start.ArgumentList.Add("--effort");
                    start.ArgumentList.Add(effort);
                }

                using var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível iniciar o claude.");
                try
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
                        return new Outcome(null, null, "Timeout do Claude Code.", 0, 0);

                    return Interpret(await stdout, await stderr);
                }
                finally
                {
                    // Timeout ou exceção: encerra o processo e espera, para a pasta poder ser apagada
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit();
                    }
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception)
            {
                return new Outcome(null, null, "claude.exe não encontrado no PATH (instalador nativo do Claude Code, com login feito).", 0, 0);
            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }

        private static Outcome Interpret(string stdout, string stderr)
        {
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(stdout);
            }
            catch (JsonException)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? "saída fora do formato" : stderr.Trim();
                return new Outcome(null, null, $"Claude Code: {message[..Math.Min(300, message.Length)]}", 0, 0);
            }

            using (json)
            {
                var root = json.RootElement;
                long input = 0, output = 0;
                if (root.TryGetProperty("usage", out var usage))
                {
                    input = Sum(usage, "input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens");
                    output = Sum(usage, "output_tokens");
                }

                if (root.TryGetProperty("stop_reason", out var stop) && stop.ValueKind == JsonValueKind.String && stop.GetString() == "refusal")
                    return new Outcome(null, AnthropicChargeExtractor.RefusedReason, null, input, output);

                if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True ||
                    !root.TryGetProperty("structured_output", out var structured) || structured.ValueKind != JsonValueKind.Object)
                {
                    var subtype = root.TryGetProperty("subtype", out var s) ? s.ToString() : "?";
                    return new Outcome(null, null, $"Claude Code sem resposta estruturada ({subtype}).", input, output);
                }

                try
                {
                    return new Outcome(AnthropicChargeExtractor.FromJson(structured.GetRawText()), null, null, input, output);
                }
                catch (ChargeExtractionException ex)
                {
                    return new Outcome(null, ex.Message, null, input, output);
                }
                catch (InvalidOperationException ex)
                {
                    return new Outcome(null, null, ex.Message, input, output);
                }
            }
        }

        private static long Sum(JsonElement usage, params string[] names) =>
            names.Sum(n => usage.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0);
    }
}
