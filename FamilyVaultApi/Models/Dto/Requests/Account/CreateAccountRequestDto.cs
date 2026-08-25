using FamilyVaultApi.Common.Validators.DtoValidators;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class CreateAccountRequestDto
    {
        [PhoneValidatorCustom]
        public string PhoneNumber { get; init; }
        public string Email { get; init; }
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
        public int Age { get; init; }
    }
}
