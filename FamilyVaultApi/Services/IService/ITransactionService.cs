using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Services.IService
{
    public interface ITransactionService
    {
        Task<TransactionResponseDto> CreateAsync(CreateTransactionDto dto);
        Task<PagedResult<TransactionResponseDto>> GetAllAsync(TransactionQueryRequestDto query);
        Task DeleteAsync(int id);
    }
}
