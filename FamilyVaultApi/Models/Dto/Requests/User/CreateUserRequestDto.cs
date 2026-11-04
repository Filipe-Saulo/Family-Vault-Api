using FamilyVaultApi.Common.Validators.DtoValidators;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class CreateUserRequestDto
    {
        [Required(ErrorMessage = "O primeiro nome é obrigatório.")]
        [StringLength(100, ErrorMessage = "O Primeiro nome deve ter no máximo 100 caracteres.")]
        public string FirstName { get; set; }
        [Required(ErrorMessage = "O Sobrenome é obrigatório.")]
        [StringLength(100, ErrorMessage = "O Sobrenome deve ter no máximo 100 caracteres.")]
        public string LastName { get; set; }
        [Range(0, 150, ErrorMessage = "A Idade deve estar entre 0 e 150.")]
        public int Age { get; set; }
        [Required(ErrorMessage = "Password é obrigatório.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        public string PasswordConfirm { get; set; }
        [Required(ErrorMessage = "O papel é obrigatório.")]
        [RegularExpression("^(Administrator|User)$", ErrorMessage = "O papel deve ser Administrator ou User.")]
        public string Role { get; set; }
        [StringLength(256, ErrorMessage = "O e-mail deve ter no máximo 256 caracteres.")]
        public string? Email { get; set; }
        [PhoneValidatorCustom]
        public string? PhoneNumber { get; set; }
    }
}
