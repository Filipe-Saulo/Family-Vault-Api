using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    public class CategoryPurposeService : ICategoryPurposeService
    {
        private readonly ICategoryPurposeRepository _repository;

        public CategoryPurposeService(ICategoryPurposeRepository repository)
        {
            _repository = repository;
        }

        public async Task<CategoryPurposeResponseDto> CreateAsync(CreateCategoryPurposeDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _repository.AddAsync(dto);
        }

        public async Task<List<CategoryPurposeResponseDto>> GetAllAsync(bool? isActive)
        {
            return await _repository.GetAllAsync(isActive);
        }

        public async Task<CategoryPurposeResponseDto> UpdateAsync(int id, UpdateCategoryPurposeDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _repository.UpdateAsync(id, dto);
        }
    }
}
