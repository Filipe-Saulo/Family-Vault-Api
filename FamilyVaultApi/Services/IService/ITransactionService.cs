using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface ITransactionService
    {
        Task<TransactionResponseDto> CreateAsync(CreateTransactionDto dto, ClaimsPrincipal userClaims);
        Task<PagedResult<TransactionResponseDto>> GetAllAsync(TransactionQueryRequestDto query);
        Task<TransactionResponseDto> UpdateAsync(int id, UpdateTransactionDto dto);
        Task DeleteAsync(int id);
    }
}
