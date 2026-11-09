using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class RegistrationCodeRequestDto
    {
        // Sem validador de formato: telefone ou e-mail inválido também responde 202, para não revelar quais contas existem
        [StringLength(30, ErrorMessage = "Telefone inválido.")]
        public string? PhoneNumber { get; set; }
        [StringLength(254, ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }
    }
}
