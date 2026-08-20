using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface ITransactionRepository
    {
        Task<TransactionResponseDto> AddAsync(CreateTransactionDto dto);
        Task<PagedResult<TransactionResponseDto>> GetAllAsync(TransactionQueryRequestDto query);
        Task<TransactionResponseDto> UpdateAsync(int id, UpdateTransactionDto dto);
        Task DeleteAsync(int id);
        Task<string?> GetOwnerUserIdAsync(int transactionId);
    }
}
