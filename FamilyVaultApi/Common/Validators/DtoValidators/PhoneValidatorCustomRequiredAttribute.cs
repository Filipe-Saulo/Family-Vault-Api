using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Common.Validators.DtoValidators
{
    public class PhoneValidatorCustomRequiredAttribute : PhoneValidatorCustomAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                ErrorMessage = "Número de telefone é obrigatório.";
                return false;
            }

            return base.IsValid(value);
        }
    }
}
