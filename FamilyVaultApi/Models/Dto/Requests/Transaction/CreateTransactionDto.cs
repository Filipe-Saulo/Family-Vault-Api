using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Transaction
{
    public class CreateTransactionDto
    {
        [StringLength(450, ErrorMessage = "O UserId deve ter no máximo 450 caracteres.")]
        public string? UserId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "A categoria é obrigatória.")]
        public int CategoryId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "O tipo de transação é obrigatório.")]
        public int TransactionTypeId { get; set; }
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
        public string Description { get; set; }
        [Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "O valor deve estar entre 0,01 e 999.999.999.")]
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}
