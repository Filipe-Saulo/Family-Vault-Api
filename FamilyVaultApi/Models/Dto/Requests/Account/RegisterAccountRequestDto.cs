using FamilyVaultApi.Common.Validators.DtoValidators;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    // Corpo do POST /api/register: exatamente um entre PhoneNumber e Email, confirmado pelo código.
    // O CreateAccountRequestDto segue só no POST /api/user.
    public class RegisterAccountRequestDto
    {
        [PhoneValidatorCustom]
        public string? PhoneNumber { get; init; }
        [StringLength(254, ErrorMessage = "E-mail inválido.")]
        public string? Email { get; init; }
        [Required(ErrorMessage = "Código é obrigatório.")]
        [StringLength(10, ErrorMessage = "Código inválido.")]
        public string Code { get; init; }
        [Required(ErrorMessage = "Password é obrigatório.")]
        public string Password { get; init; }
        [StringLength(30, ErrorMessage = "O Primeiro nome deve ter no máximo 30 caracteres.")]
        [Required(ErrorMessage = "O primeiro nome é obrigatório.")]
        public string FirstName { get; init; }
        [StringLength(70, ErrorMessage = "O Sobrenome deve ter no máximo 70 caracteres.")]
        [Required(ErrorMessage = "O Sobrenome é obrigatório.")]
        public string LastName { get; init; }
        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        public string PasswordConfirm { get; init; }

        [Required(ErrorMessage = "A Idade é obrigatória")]
        [Range(0, 150, ErrorMessage = "A Idade deve estar entre 0 e 150.")]
        public int Age { get; init; }
    }
}
