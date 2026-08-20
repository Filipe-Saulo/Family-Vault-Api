using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface ITransactionTypeRepository
    {
        Task<TransactionTypeResponseDto> AddAsync(CreateTransactionTypeDto dto);
        Task<List<TransactionTypeResponseDto>> GetAllAsync(bool? isActive);
        Task<TransactionTypeResponseDto> UpdateAsync(int id, UpdateTransactionTypeDto dto);
        Task<TransactionTypeCode?> GetCodeAsync(int transactionTypeId);
    }
}
