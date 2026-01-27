using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _repository;

        public TransactionService(ITransactionRepository repository)
        {
            _repository = repository;
        }

        public async Task<TransactionResponseDto> CreateAsync(CreateTransactionDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _repository.AddAsync(dto);
        }

        public async Task<PagedResult<TransactionResponseDto>> GetAllAsync(TransactionQueryRequestDto query)
        {
            query ??= new TransactionQueryRequestDto();
            return await _repository.GetAllAsync(query);
        }

        public async Task DeleteAsync(int id)
        {
            await _repository.DeleteAsync(id);
        }
    }
}
