using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Charge
{
    // Informar o valor de um PDF com senha: os campos do confirmar e o emissor (vazio = remetente do e-mail)
    public class ManualChargeDto : ConfirmChargeDto
    {
        [StringLength(200, ErrorMessage = "O emissor deve ter no máximo 200 caracteres.")]
        public string? Issuer { get; set; }
    }
}
