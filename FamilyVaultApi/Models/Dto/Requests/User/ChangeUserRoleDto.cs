using FamilyVaultApi.Common.Validators.DtoValidators;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class ChangeUserRoleDto
    {
        [Required(ErrorMessage = "O papel é obrigatório.")]
        [RegularExpression("^(Administrator|User)$", ErrorMessage = "O papel deve ser Administrator ou User.")]
        public string Role { get; set; }

        // Só usados quando a conta ainda não tem o dado de login do novo papel
        [StringLength(256, ErrorMessage = "O e-mail deve ter no máximo 256 caracteres.")]
        public string? Email { get; set; }
        [PhoneValidatorCustom]
        public string? PhoneNumber { get; set; }
    }
}
