using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;

namespace FamilyVaultApi.Services.IService
{
    public interface ITransactionTypeService
    {
        Task<TransactionTypeResponseDto> CreateAsync(CreateTransactionTypeDto dto);
        Task<List<TransactionTypeResponseDto>> GetAllAsync(bool? isActive);
        Task<TransactionTypeResponseDto> UpdateAsync(int id, UpdateTransactionTypeDto dto);
    }
}
