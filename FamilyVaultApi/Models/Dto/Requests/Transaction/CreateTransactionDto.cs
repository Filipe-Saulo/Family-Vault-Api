namespace FamilyVaultApi.Models.Dto.Requests.Transaction
{
    public class CreateTransactionDto
    {
        public string UserId { get; set; }
        public int CategoryId { get; set; }
        public int TransactionTypeId { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}
