using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace FamilyVaultApi.Common.Validators.DtoValidators
{
    public class PhoneValidatorCustomAttribute : ValidationAttribute
    {
        private static readonly Regex ShapeRegex = new(@"^[\d+()\-\s]{8,17}$", RegexOptions.Compiled);

        // Mensagem fixa no construtor: a instância do atributo é compartilhada entre requisições.
        public PhoneValidatorCustomAttribute() : base("Número de telefone inválido.")
        {
        }

        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return true;

            return ShapeRegex.IsMatch(value.ToString()!);
        }
    }
}
