using System.Text;
using FamilyVaultApi.Services.Service;
using FluentAssertions;

namespace FamilyVaultApi.UnitTests.Services
{
    public class ChargeValidatorTests
    {
        // ---------- Geração de linhas digitáveis (DVs calculados aqui, conforme FEBRABAN) ----------

        // Módulo 10: pesos 2,1,2,1... da direita para a esquerda; soma os dígitos dos produtos
        private static int Mod10(string digits)
        {
            var sum = 0;
            var weight = 2;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var product = (digits[i] - '0') * weight;
                sum += product > 9 ? product - 9 : product;
                weight = weight == 2 ? 1 : 2;
            }
            return (10 - sum % 10) % 10;
        }

        // Módulo 11: pesos 2..9 da direita para a esquerda
        private static int Mod11Sum(string digits)
        {
            var sum = 0;
            var weight = 2;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                sum += (digits[i] - '0') * weight;
                weight = weight == 9 ? 2 : weight + 1;
            }
            return sum % 11;
        }

        // DV geral da bancária: 11 - resto; 0, 10 ou 11 vira 1
        private static int BankGeneralDv(string barcode43)
        {
            var dv = 11 - Mod11Sum(barcode43);
            return dv is 0 or 10 or 11 ? 1 : dv;
        }

        // Arrecadação, módulo 11 por bloco/geral: resto 0 ou 1 vira 0; resto 10 vira 1; senão 11 - resto
        private static int CollectionMod11(string digits)
        {
            var rest = Mod11Sum(digits);
            if (rest is 0 or 1) return 0;
            return rest == 10 ? 1 : 11 - rest;
        }

        // Linha digitável bancária de 47 dígitos, sem pontuação
        private static string BuildBankLine(
            string bank = "341", string currency = "9", string free25 = "1090000000123456789012345",
            string factor = "9876", string value = "0000012345",
            Func<int, int>? tamperField1 = null, Func<int, int>? tamperField2 = null,
            Func<int, int>? tamperField3 = null, Func<int, int>? tamperGeneral = null)
        {
            free25.Length.Should().Be(25);
            var barcode43 = bank + currency + factor + value + free25;
            var general = BankGeneralDv(barcode43);

            var f1 = bank + currency + free25[..5];
            var f2 = free25.Substring(5, 10);
            var f3 = free25.Substring(15, 10);

            var dv1 = Mod10(f1);
            var dv2 = Mod10(f2);
            var dv3 = Mod10(f3);

            dv1 = tamperField1?.Invoke(dv1) ?? dv1;
            dv2 = tamperField2?.Invoke(dv2) ?? dv2;
            dv3 = tamperField3?.Invoke(dv3) ?? dv3;
            general = tamperGeneral?.Invoke(general) ?? general;

            var line = f1 + dv1 + f2 + dv2 + f3 + dv3 + general + factor + value;
            line.Length.Should().Be(47);
            return line;
        }

        // Linha de arrecadação de 48 dígitos; indicator = 6/7 (módulo 10) ou 8/9 (módulo 11)
        private static string BuildCollectionLine(
            char indicator, string payload40 = "0000012345600000000000000000001234567890",
            Func<int, int>? tamperGeneral = null, int? tamperBlock = null)
        {
            payload40.Length.Should().Be(40);
            // código de 44: 8, segmento 1, indicador, DV geral, 40 dígitos (valor + empresa + livre)
            var withoutDv = "81" + indicator + payload40;
            var mod10 = indicator is '6' or '7';
            var general = mod10 ? Mod10(withoutDv) : CollectionMod11(withoutDv);
            general = tamperGeneral?.Invoke(general) ?? general;

            var barcode44 = "81" + indicator + general + payload40;
            var line = new StringBuilder();
            for (var block = 0; block < 4; block++)
            {
                var data = barcode44.Substring(block * 11, 11);
                var dv = mod10 ? Mod10(data) : CollectionMod11(data);
                if (tamperBlock == block) dv = (dv + 1) % 10;
                line.Append(data).Append(dv);
            }
            line.Length.Should().Be(48);
            return line.ToString();
        }

        private static string Pretty47(string line) =>
            $"{line[..5]}.{line.Substring(5, 5)} {line.Substring(10, 5)}.{line.Substring(15, 6)} " +
            $"{line.Substring(21, 5)}.{line.Substring(26, 6)} {line[32]} {line[33..]}";

        private static string Pretty48(string line) =>
            $"{line[..12]} {line.Substring(12, 12)} {line.Substring(24, 12)} {line.Substring(36, 12)}";

        // ---------- Sanidade do helper: exemplo conhecido (boleto de exemplo FEBRABAN, conferido à mão) ----------

        [Fact]
        public void Helper_Mod10_ShouldMatchHandCheckedExamples()
        {
            Mod10("1").Should().Be(8);          // 1*2=2 -> 10-2
            Mod10("12").Should().Be(5);         // 2*2=4 + 1*1=1 -> 10-5
            Mod10("001905009").Should().Be(5);  // campo 1 de um boleto do Banco do Brasil: 00190.50095
        }

        // ---------- NormalizeBarcode: bancária ----------

        [Fact]
        public void NormalizeBarcode_BankLineValidWithoutPunctuation_ShouldReturnDigits()
        {
            var line = BuildBankLine();

            ChargeValidator.NormalizeBarcode(line).Should().Be(line);
        }

        [Fact]
        public void NormalizeBarcode_BankLineValidWithPunctuationAndSpaces_ShouldReturnOnlyDigits()
        {
            var line = BuildBankLine();

            ChargeValidator.NormalizeBarcode(Pretty47(line)).Should().Be(line);
            ChargeValidator.NormalizeBarcode("  " + Pretty47(line) + "\n").Should().Be(line);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void NormalizeBarcode_BankLineWithWrongFieldDv_ShouldReturnNull(int field)
        {
            Func<int, int> wrong = dv => (dv + 1) % 10;
            var line = BuildBankLine(
                tamperField1: field == 1 ? wrong : null,
                tamperField2: field == 2 ? wrong : null,
                tamperField3: field == 3 ? wrong : null);

            ChargeValidator.NormalizeBarcode(line).Should().BeNull();
        }

        [Fact]
        public void NormalizeBarcode_BankLineWithWrongGeneralDv_ShouldReturnNull()
        {
            // Campos continuam corretos: só o DV geral (posição 33) está errado
            var line = BuildBankLine(tamperGeneral: dv => dv % 9 + 1);

            ChargeValidator.NormalizeBarcode(line).Should().BeNull();
        }

        [Fact]
        public void NormalizeBarcode_BankLineWithTamperedAmount_ShouldReturnNull()
        {
            // Mudar um dígito do valor (fora dos campos 1-3) quebra o DV geral
            var line = BuildBankLine();
            var tampered = line[..46] + (line[46] == '9' ? '8' : (char)(line[46] + 1));

            ChargeValidator.NormalizeBarcode(tampered).Should().BeNull();
        }

        [Fact]
        public void NormalizeBarcode_BankLineWithGeneralDvRuleOne_ShouldBeAccepted()
        {
            // Procura um código cujo DV geral calculado caia na regra "0/10/11 vira 1"
            string? line = null;
            for (var i = 0; i < 2000 && line is null; i++)
            {
                var free = "1" + i.ToString().PadLeft(24, '0');
                var barcode43 = "3419" + "9876" + "0000012345" + free;
                if (11 - Mod11Sum(barcode43) is 10 or 11)
                    line = BuildBankLine(free25: free);
            }

            line.Should().NotBeNull();
            ChargeValidator.NormalizeBarcode(line).Should().Be(line);
        }

        // ---------- NormalizeBarcode: arrecadação ----------

        [Theory]
        [InlineData('6')]
        [InlineData('7')]
        public void NormalizeBarcode_CollectionLineMod10Valid_ShouldReturnDigits(char indicator)
        {
            var line = BuildCollectionLine(indicator);

            ChargeValidator.NormalizeBarcode(line).Should().Be(line);
            ChargeValidator.NormalizeBarcode(Pretty48(line).Replace(" ", "-")).Should().Be(line);
        }

        [Theory]
        [InlineData('8')]
        [InlineData('9')]
        public void NormalizeBarcode_CollectionLineMod11Valid_ShouldReturnDigits(char indicator)
        {
            var line = BuildCollectionLine(indicator);

            ChargeValidator.NormalizeBarcode(line).Should().Be(line);
            ChargeValidator.NormalizeBarcode(Pretty48(line)).Should().Be(line);
        }

        [Theory]
        [InlineData('6', 0)]
        [InlineData('6', 3)]
        [InlineData('7', 2)]
        [InlineData('8', 1)]
        [InlineData('9', 3)]
        public void NormalizeBarcode_CollectionLineWithWrongBlockDv_ShouldReturnNull(char indicator, int block)
        {
            var line = BuildCollectionLine(indicator, tamperBlock: block);

            ChargeValidator.NormalizeBarcode(line).Should().BeNull();
        }

        [Theory]
        [InlineData('6')]
        [InlineData('8')]
        public void NormalizeBarcode_CollectionLineWithWrongGeneralDv_ShouldReturnNull(char indicator)
        {
            // O DV geral (posição 4 do código de 44) errado, com os DVs dos blocos recalculados sobre ele
            var line = BuildCollectionLine(indicator, tamperGeneral: dv => (dv + 1) % 10);

            ChargeValidator.NormalizeBarcode(line).Should().BeNull();
        }

        // ---------- NormalizeBarcode: tamanhos e entradas vazias ----------

        [Fact]
        public void NormalizeBarcode_RawBarcodeOf44Digits_ShouldReturnNull()
        {
            var line = BuildBankLine();
            // código de barras cru de 44 dígitos remontado a partir da linha
            var barcode44 = line[..4] + line[32] + line[33..] + line.Substring(4, 5) + line.Substring(10, 10) + line.Substring(21, 10);
            barcode44.Length.Should().Be(44);

            ChargeValidator.NormalizeBarcode(barcode44).Should().BeNull();
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("123456789012345678901234567890123456789012345678901")]
        [InlineData("abc")]
        public void NormalizeBarcode_OtherLengthsOrNoDigits_ShouldReturnNull(string raw)
        {
            ChargeValidator.NormalizeBarcode(raw).Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizeBarcode_NullOrEmpty_ShouldReturnNull(string? raw)
        {
            ChargeValidator.NormalizeBarcode(raw).Should().BeNull();
        }

        [Fact]
        public void NormalizeBarcode_Of48DigitsNotStartingWith8_ShouldReturnNull()
        {
            var line = BuildCollectionLine('6');
            var notCollection = "7" + line[1..];

            ChargeValidator.NormalizeBarcode(notCollection).Should().BeNull();
        }

        // ---------- ValidateAmount ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-0.01)]
        public void ValidateAmount_ZeroOrNegative_ShouldReturnNull(double amount)
        {
            ChargeValidator.ValidateAmount((decimal)amount).Should().BeNull();
        }

        [Fact]
        public void ValidateAmount_Null_ShouldReturnNull()
        {
            ChargeValidator.ValidateAmount(null).Should().BeNull();
        }

        [Theory]
        [InlineData(0.01)]
        [InlineData(150.75)]
        [InlineData(999999999)]
        public void ValidateAmount_PositiveUpToCeiling_ShouldReturnSameValue(double amount)
        {
            ChargeValidator.ValidateAmount((decimal)amount).Should().Be((decimal)amount);
        }

        [Fact]
        public void ValidateAmount_AboveCeiling_ShouldReturnNull()
        {
            ChargeValidator.ValidateAmount(999999999.01m).Should().BeNull();
            ChargeValidator.ValidateAmount(1000000000m).Should().BeNull();
        }

        // ---------- ValidateDueDate ----------

        private static readonly DateTime Received = new(2026, 11, 10, 15, 30, 0, DateTimeKind.Utc);

        [Fact]
        public void ValidateDueDate_InsideWindow_ShouldReturnOnlyTheDate()
        {
            var result = ChargeValidator.ValidateDueDate(new DateTime(2026, 12, 5, 18, 45, 10), Received);

            result.Should().Be(new DateTime(2026, 12, 5));
            result!.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void ValidateDueDate_ExactlyOneYearBeforeAndAfter_ShouldBeAccepted()
        {
            ChargeValidator.ValidateDueDate(new DateTime(2025, 11, 10), Received).Should().Be(new DateTime(2025, 11, 10));
            ChargeValidator.ValidateDueDate(new DateTime(2027, 11, 10), Received).Should().Be(new DateTime(2027, 11, 10));
        }

        [Fact]
        public void ValidateDueDate_OneDayOutsideTheLimits_ShouldReturnNull()
        {
            ChargeValidator.ValidateDueDate(new DateTime(2025, 11, 9), Received).Should().BeNull();
            ChargeValidator.ValidateDueDate(new DateTime(2027, 11, 11), Received).Should().BeNull();
        }

        [Fact]
        public void ValidateDueDate_FarOutside_ShouldReturnNull()
        {
            ChargeValidator.ValidateDueDate(new DateTime(2020, 1, 1), Received).Should().BeNull();
            ChargeValidator.ValidateDueDate(new DateTime(2035, 1, 1), Received).Should().BeNull();
        }

        [Fact]
        public void ValidateDueDate_Null_ShouldReturnNull()
        {
            ChargeValidator.ValidateDueDate(null, Received).Should().BeNull();
        }
    }
}
