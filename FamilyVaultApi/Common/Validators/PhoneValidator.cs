using System.Text.RegularExpressions;

namespace FamilyVaultApi.Common.Validators
{
    public static class PhoneValidator
    {
        public static bool ValidarCelularBr(string phoneNumber, out string numeroFormatado)
        {
            numeroFormatado = null;

            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove todos os caracteres não numéricos
            string digitsOnly = Regex.Replace(phoneNumber, @"[^\d]", "");

            // Remove prefixo internacional "55" se existir
            if (digitsOnly.StartsWith("55"))
                digitsOnly = digitsOnly.Substring(2);

            // Agora esperamos 11 dígitos: 2 do DDD e 9 do número
            if (digitsOnly.Length != 11)
                return false;

            // Validação de DDDs brasileiros válidos
            var dddsValidos = new[] {
            11, 12, 13, 14, 15, 16, 17, 18, 19,
            21, 22, 24, 27, 28,
            31, 32, 33, 34, 35, 37, 38,
            41, 42, 43, 44, 45, 46, 47, 48, 49,
            51, 53, 54, 55,
            61, 62, 63, 64, 65, 66, 67, 68, 69,
            71, 73, 74, 75, 77, 79,
            81, 82, 83, 84, 85, 86, 87, 88, 89,
            91, 92, 93, 94, 95, 96, 97, 98, 99
        };

            if (!int.TryParse(digitsOnly.Substring(0, 2), out int ddd) || !dddsValidos.Contains(ddd))
                return false;

            string numeroSemDDD = digitsOnly.Substring(2);

            // Número deve começar com 9
            if (numeroSemDDD[0] != '9')
                return false;

            // Verifica se todos os dígitos são iguais
            if (numeroSemDDD.Distinct().Count() == 1)
                return false;

            // Verifica padrões inválidos no final
            if (numeroSemDDD.EndsWith("0000") || numeroSemDDD.EndsWith("1234") || numeroSemDDD.EndsWith("4321"))
                return false;

            numeroFormatado = "55" + digitsOnly;
            return true;
        }

        public static bool ValidarCelularInternacional(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            string digitsOnly = Regex.Replace(phoneNumber, @"[^\d]", "");

            return Regex.IsMatch(digitsOnly, @"^[1-9]\d{7,14}$");
        }
    }
}
