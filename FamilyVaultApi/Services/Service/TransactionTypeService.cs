using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    public class TransactionTypeService : ITransactionTypeService
    {
        private readonly ITransactionTypeRepository _repository;

        public TransactionTypeService(ITransactionTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<TransactionTypeResponseDto> CreateAsync(CreateTransactionTypeDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _repository.AddAsync(dto);
        }

        public async Task<List<TransactionTypeResponseDto>> GetAllAsync(bool? isActive)
        {
            return await _repository.GetAllAsync(isActive);
        }

        public async Task<TransactionTypeResponseDto> UpdateAsync(int id, UpdateTransactionTypeDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _repository.UpdateAsync(id, dto);
        }
    }
}
