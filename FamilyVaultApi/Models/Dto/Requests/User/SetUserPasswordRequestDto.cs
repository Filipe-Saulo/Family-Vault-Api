using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class SetUserPasswordRequestDto
    {
        [Required(ErrorMessage = "Senha é obrigatória.")]
        [StringLength(100, ErrorMessage = "Senha deve ter no máximo 100 caracteres.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Confirmação de senha é obrigatória.")]
        public string PasswordConfirm { get; set; }
    }
}
