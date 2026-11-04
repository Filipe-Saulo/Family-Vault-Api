using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class ForgotPasswordRequestDto
    {
        // Sem validador de telefone: o formato inválido também responde 202, para não revelar quais contas existem
        [Required(ErrorMessage = "Telefone é obrigatório.")]
        [StringLength(30, ErrorMessage = "Telefone inválido.")]
        public string Phone { get; set; }
    }
}
