using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Responses.TransactionType
{
    public class TransactionTypeSimpleDto
    {
        public int TransactionTypeId { get; set; }
        public string Name { get; set; }
        public TransactionTypeCode Code { get; set; }
    }
}
