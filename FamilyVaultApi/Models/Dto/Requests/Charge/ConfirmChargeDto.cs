using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Charge
{
    public class ConfirmChargeDto
    {
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
        public string Description { get; set; }
        [Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "O valor deve estar entre 0,01 e 999.999.999.")]
        public decimal Amount { get; set; }
        [Required(ErrorMessage = "O vencimento é obrigatório.")]
        public DateTime? DueDate { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "A categoria é obrigatória.")]
        public int CategoryId { get; set; }
    }
}
