using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Common.Validators.DtoValidators
{
    public class PhoneValidatorCustomRequiredAttribute : PhoneValidatorCustomAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return false;

            return base.IsValid(value);
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                var members = validationContext.MemberName == null ? null : new[] { validationContext.MemberName };
                return new ValidationResult("Número de telefone é obrigatório.", members);
            }

            return base.IsValid(value, validationContext);
        }
    }
}
