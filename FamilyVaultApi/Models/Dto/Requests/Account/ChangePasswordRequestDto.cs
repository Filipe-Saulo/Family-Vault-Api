using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class ChangePasswordRequestDto
    {
        [Required(ErrorMessage = "Senha atual é obrigatória.")]
        public string CurrentPassword { get; set; }
        [Required(ErrorMessage = "Senha é obrigatória.")]
        [StringLength(100, ErrorMessage = "Senha deve ter no máximo 100 caracteres.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Confirmação de senha é obrigatória.")]
        public string PasswordConfirm { get; set; }
    }
}
