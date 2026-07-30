namespace FamilyVaultApi.Models.Dto.Requests.TransactionType
{
    public class UpdateTransactionTypeDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
