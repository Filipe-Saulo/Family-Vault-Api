using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Charge
{
    public class SubmitDocumentPasswordDto
    {
        [Required(ErrorMessage = "A senha é obrigatória.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "A senha deve ter entre 1 e 100 caracteres.")]
        public string Password { get; set; } = string.Empty;
        // Só vale para documento do Gmail com remetente
        public bool RememberForSender { get; set; }
    }
}
