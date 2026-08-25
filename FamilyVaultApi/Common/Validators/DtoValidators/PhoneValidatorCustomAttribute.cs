using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace FamilyVaultApi.Common.Validators.DtoValidators
{
    public class PhoneValidatorCustomAttribute : ValidationAttribute
    {
        private static readonly Regex ShapeRegex = new(@"^[\d+()\-\s]{8,17}$", RegexOptions.Compiled);

        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return true;

            var isValid = ShapeRegex.IsMatch(value.ToString()!);

            if (!isValid)
                ErrorMessage = "Número de telefone inválido.";

            return isValid;
        }
    }
}
