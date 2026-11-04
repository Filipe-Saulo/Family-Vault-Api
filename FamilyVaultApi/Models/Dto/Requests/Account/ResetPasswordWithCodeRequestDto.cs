using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class ResetPasswordWithCodeRequestDto
    {
        [Required(ErrorMessage = "Telefone é obrigatório.")]
        [StringLength(30, ErrorMessage = "Telefone inválido.")]
        public string Phone { get; set; }
        [Required(ErrorMessage = "Código é obrigatório.")]
        [StringLength(10, ErrorMessage = "Código inválido.")]
        public string Code { get; set; }
        [Required(ErrorMessage = "Senha é obrigatória.")]
        [StringLength(100, ErrorMessage = "Senha deve ter no máximo 100 caracteres.")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Confirmação de senha é obrigatória.")]
        public string PasswordConfirm { get; set; }
    }
}
