namespace FamilyVaultApi.Services.Service
{
    // O código valida o que a IA propõe: nada do que o extrator devolve é aceito sem passar por aqui
    public static class ChargeValidator
    {
        private const decimal MaxAmount = 999999999m;

        // Devolve só os dígitos se for linha digitável válida (bancária de 47 ou arrecadação de 48), senão null
        public static string? NormalizeBarcode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());

            return digits.Length switch
            {
                47 => IsValidBankLine(digits) ? digits : null,
                48 => IsValidCollectionLine(digits) ? digits : null,
                _ => null
            };
        }

        public static decimal? ValidateAmount(decimal? amount)
        {
            if (amount is null || amount <= 0 || amount > MaxAmount)
                return null;

            return amount;
        }

        // Só a data; plausível = de 1 ano antes a 1 ano depois do recebimento
        public static DateTime? ValidateDueDate(DateTime? dueDate, DateTime receivedAt)
        {
            if (dueDate is null)
                return null;

            var date = dueDate.Value.Date;
            var reference = receivedAt.Date;

            if (date < reference.AddYears(-1) || date > reference.AddYears(1))
                return null;

            return date;
        }

        // Trim, vazio vira null e corta no tamanho da coluna
        public static string? NormalizeText(string? text, int maxLength)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            return trimmed.Length > maxLength ? trimmed[..maxLength].TrimEnd() : trimmed;
        }

        private static bool IsValidBankLine(string line)
        {
            // Três campos com DV módulo 10: 9 dígitos + DV, 10 + DV, 10 + DV
            if (Mod10(line[..9]) != Digit(line[9])
                || Mod10(line[10..20]) != Digit(line[20])
                || Mod10(line[21..31]) != Digit(line[31]))
                return false;

            // Código de barras de 44 dígitos remontado; o DV geral (posição 33 da linha) vai na posição 5
            var withoutDv = line[..4] + line[33..47] + line[4..9] + line[10..20] + line[21..31];
            return Mod11Bank(withoutDv) == Digit(line[32]);
        }

        private static bool IsValidCollectionLine(string line)
        {
            if (line[0] != '8')
                return false;

            // 3º dígito: 6 e 7 = módulo 10; 8 e 9 = módulo 11
            var useMod10 = line[2] is '6' or '7';
            var useMod11 = line[2] is '8' or '9';
            if (!useMod10 && !useMod11)
                return false;

            int Dv(string value) => useMod10 ? Mod10(value) : Mod11Collection(value);

            var barcode = string.Empty;
            for (var block = 0; block < 4; block++)
            {
                var data = line.Substring(block * 12, 11);
                if (Dv(data) != Digit(line[block * 12 + 11]))
                    return false;

                barcode += data;
            }

            // DV geral do código de 44 dígitos (posição 4)
            return Dv(barcode[..3] + barcode[4..]) == Digit(barcode[3]);
        }

        private static int Digit(char c) => c - '0';

        // Pesos 2,1,2,1... da direita para a esquerda; produtos acima de 9 somam os dígitos
        private static int Mod10(string digits)
        {
            var sum = 0;
            var weight = 2;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var product = Digit(digits[i]) * weight;
                sum += product > 9 ? product - 9 : product;
                weight = weight == 2 ? 1 : 2;
            }

            return (10 - sum % 10) % 10;
        }

        // Pesos 2 a 9 da direita para a esquerda
        private static int Mod11Sum(string digits)
        {
            var sum = 0;
            var weight = 2;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                sum += Digit(digits[i]) * weight;
                weight = weight == 9 ? 2 : weight + 1;
            }

            return sum;
        }

        // Bancária: DV 0, 10 ou 11 vira 1
        private static int Mod11Bank(string digits)
        {
            var dv = 11 - Mod11Sum(digits) % 11;
            return dv is 0 or 10 or 11 ? 1 : dv;
        }

        // Arrecadação: resto 0 ou 1 dá DV 0; resto 10 dá DV 1
        private static int Mod11Collection(string digits)
        {
            var remainder = Mod11Sum(digits) % 11;
            return remainder is 0 or 1 ? 0 : 11 - remainder;
        }
    }
}
