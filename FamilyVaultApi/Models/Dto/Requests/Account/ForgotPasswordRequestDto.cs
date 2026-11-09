using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class ForgotPasswordRequestDto
    {
        // Exatamente um entre Phone e Email (conferido no service). Sem validador de formato: o formato
        // inválido também responde 202, para não revelar quais contas existem
        [StringLength(30, ErrorMessage = "Telefone inválido.")]
        public string? Phone { get; set; }
        [StringLength(254, ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }
    }
}
