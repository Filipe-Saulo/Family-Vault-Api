using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Requests.TransactionType
{
    public class CreateTransactionTypeDto
    {
        public TransactionTypeCode Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
