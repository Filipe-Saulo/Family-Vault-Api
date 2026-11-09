using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class ResetPasswordWithCodeRequestDto
    {
        // Exatamente um entre Phone e Email (conferido no service)
        [StringLength(30, ErrorMessage = "Telefone inválido.")]
        public string? Phone { get; set; }
        [StringLength(254, ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }
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
