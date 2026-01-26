using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Common.Validators.DtoValidators
{
    public class PhoneValidatorCustomAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return false;

            string numeroFormatado;
            var isValid = PhoneValidator.ValidarCelularBr(value.ToString(), out numeroFormatado);

            if (!isValid)
            {
                ErrorMessage = "Número de celular inválido. O número deve ter 11 dígitos e começar com '9' após o DDD.";
            }

            return isValid;
        }
    }
}
