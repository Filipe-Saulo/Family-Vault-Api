using System.Globalization;
using System.Text;
using System.Text.Json;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.Service;

namespace FamilyVaultApi.Evals
{
    public class Dataset
    {
        public List<ChargeCategoryOption> Categories { get; init; } = [];
        public List<EvalCase> Cases { get; init; } = [];
        public string FilesDirectory { get; init; } = "";

        public static Dataset Load(string directory)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "cases.json")));
            var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var dataset = new Dataset
            {
                Categories = json.RootElement.GetProperty("categories").Deserialize<List<ChargeCategoryOption>>(web)!,
                Cases = json.RootElement.GetProperty("cases").Deserialize<List<EvalCase>>(web)!,
                FilesDirectory = Path.Combine(directory, "files")
            };

            // O gabarito tem de passar no mesmo validador da API; senão o caso está errado, não o modelo
            foreach (var evalCase in dataset.Cases.Where(c => c.Expected.Barcode != null))
                if (ChargeValidator.NormalizeBarcode(evalCase.Expected.Barcode) == null)
                    throw new InvalidOperationException($"Linha digitável inválida no gabarito do caso {evalCase.Id}.");

            return dataset;
        }

        public Dataset Only(List<string> ids)
        {
            var unknown = ids.Except(Cases.Select(c => c.Id)).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException($"Caso desconhecido: {string.Join(", ", unknown)}.");
            return new Dataset { Categories = Categories, FilesDirectory = FilesDirectory, Cases = Cases.Where(c => ids.Contains(c.Id)).ToList() };
        }

        public ChargeExtractionInput InputFor(EvalCase evalCase)
        {
            var extension = Path.GetExtension(evalCase.File).ToLowerInvariant();
            return new ChargeExtractionInput
            {
                Content = File.ReadAllBytes(Path.Combine(FilesDirectory, evalCase.File)),
                ContentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => throw new InvalidOperationException($"Extensão sem tipo: {evalCase.File}.")
                },
                ExpenseCategories = Categories
            };
        }
    }

    public class EvalCase
    {
        public string Id { get; init; } = "";
        // charge, not_charge, illegible, injection
        public string Kind { get; init; } = "";
        public string File { get; init; } = "";
        public string Note { get; init; } = "";
        public Expected Expected { get; init; } = new();
    }

    public class Expected
    {
        public bool IsCharge { get; init; }
        public bool Illegible { get; init; }
        public List<string> IssuerKeywords { get; init; } = [];
        public decimal? Amount { get; init; }
        public string? DueDate { get; init; }
        public string? Barcode { get; init; }
        public List<int> CategoryIds { get; init; } = [];
    }

    public class CaseResult
    {
        public string CaseId { get; init; } = "";
        public string Kind { get; init; } = "";
        public int Round { get; set; }
        public bool Passed { get; init; }
        public string Detail { get; init; } = "";
        // Por campo (só nos casos em que o esperado é uma conta): null = não se aplica
        public bool? IsChargeOk { get; init; }
        public bool? IssuerOk { get; init; }
        public bool? AmountOk { get; init; }
        public bool? DueDateOk { get; init; }
        public bool? BarcodeOk { get; init; }
        public bool? CategoryOk { get; init; }
        // Ilegível: devolveu a falha "ilegível"; e, ilegível ou não, não inventou valor nem linha
        public bool? IllegibleDetected { get; init; }
        public bool? NoInventedData { get; init; }
        public string? Error { get; init; }
        public object? Got { get; init; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public double Seconds { get; set; }
    }

    public static class Scoring
    {
        public static CaseResult Score(EvalCase evalCase, ChargeExtractionResult? got, string? failure, string? error)
        {
            var expected = evalCase.Expected;
            var gotView = got == null ? (object?)(failure != null ? new { failure } : null) : new
            {
                got.IsCharge,
                got.Issuer,
                got.Amount,
                DueDate = got.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                got.Barcode,
                got.SuggestedCategoryId
            };

            if (error != null)
                return new CaseResult { CaseId = evalCase.Id, Kind = evalCase.Kind, Passed = false, Error = error, Detail = $"erro: {error}" };

            if (expected.Illegible)
            {
                var detected = failure == AnthropicChargeExtractor.IllegibleReason;
                var noInvented = got == null || !got.IsCharge || (got.Amount == null && got.Barcode == null);
                return new CaseResult
                {
                    CaseId = evalCase.Id, Kind = evalCase.Kind, Passed = detected, Got = gotView,
                    IllegibleDetected = detected, NoInventedData = noInvented,
                    Detail = detected ? "ilegível detectado" : noInvented ? "não marcou ilegível, mas não inventou dados" : "INVENTOU dados"
                };
            }

            if (!expected.IsCharge)
            {
                var ok = got is { IsCharge: false };
                return new CaseResult
                {
                    CaseId = evalCase.Id, Kind = evalCase.Kind, Passed = ok, Got = gotView, IsChargeOk = ok,
                    Detail = ok ? "não é conta" : failure != null ? $"falha: {failure}" : "classificou como conta"
                };
            }

            if (got == null)
                return new CaseResult
                {
                    CaseId = evalCase.Id, Kind = evalCase.Kind, Passed = false, Got = gotView, IsChargeOk = false,
                    IssuerOk = false, AmountOk = false, DueDateOk = false, BarcodeOk = false, CategoryOk = false,
                    Detail = $"falha: {failure}"
                };

            var isCharge = got.IsCharge;
            var issuer = isCharge && IssuerMatches(got.Issuer, expected.IssuerKeywords);
            var amount = isCharge && got.Amount == expected.Amount;
            var dueDate = isCharge && got.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == expected.DueDate;
            var barcode = isCharge && Digits(got.Barcode) == expected.Barcode;
            var category = isCharge && got.SuggestedCategoryId.HasValue && expected.CategoryIds.Contains(got.SuggestedCategoryId.Value);

            var misses = new List<string>();
            if (!isCharge) misses.Add("isCharge");
            if (isCharge && !issuer) misses.Add($"emissor ({got.Issuer})");
            if (isCharge && !amount) misses.Add($"valor ({got.Amount})");
            if (isCharge && !dueDate) misses.Add($"vencimento ({got.DueDate:yyyy-MM-dd})");
            if (isCharge && !barcode) misses.Add("linha digitável");
            if (isCharge && !category) misses.Add($"categoria ({got.SuggestedCategoryId})");

            return new CaseResult
            {
                CaseId = evalCase.Id, Kind = evalCase.Kind, Passed = misses.Count == 0, Got = gotView,
                IsChargeOk = isCharge, IssuerOk = issuer, AmountOk = amount, DueDateOk = dueDate, BarcodeOk = barcode, CategoryOk = category,
                Detail = misses.Count == 0 ? "todos os campos" : "errou: " + string.Join(", ", misses)
            };
        }

        private static string? Digits(string? value) => value == null ? null : new string(value.Where(char.IsAsciiDigit).ToArray());

        private static bool IssuerMatches(string? issuer, List<string> keywords) =>
            issuer != null && keywords.Any(k => Normalize(issuer).Contains(Normalize(k), StringComparison.Ordinal));

        private static string Normalize(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        }
    }
}
