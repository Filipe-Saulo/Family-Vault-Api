using FamilyVaultApi.Common;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository repository)
        {
            _categoryRepository = repository;
        }

        public async Task<CategoryResponseDto> CreateAsync(CreateCategoryDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            // Administrator cria categoria global; User cria categoria pessoal
            var ownerUserId = userClaims.IsInRole("Administrator") ? null : GetCallerId(userClaims);

            return await _categoryRepository.AddAsync(dto, ownerUserId);
        }

        public async Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query, ClaimsPrincipal userClaims)
        {
            query ??= new CategoryQueryRequestDto();

            if (query.PageNumber <= 0) query.PageNumber = 1;
            if (query.PageSize <= 0) query.PageSize = 20;
            else if (query.PageSize > 100) query.PageSize = 100;

            return await _categoryRepository.GetAllAsync(query, GetVisibilityUserId(userClaims));
        }

        public async Task<List<CategoryResponseDto>> GetLookupAsync(ClaimsPrincipal userClaims)
        {
            return await _categoryRepository.GetLookupAsync(GetVisibilityUserId(userClaims));
        }

        public async Task<CategoryResponseDto> UpdateAsync(int id, UpdateCategoryDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var info = await EnsureCanModifyCategoryAsync(id, userClaims);

            if (dto.CategoryPurposeId != info.CategoryPurposeId && await _categoryRepository.CountTransactionsAsync(id) > 0)
                throw new ConflictException("Não é possível trocar a finalidade de uma categoria em uso.");

            return await _categoryRepository.UpdateAsync(id, dto);
        }

        public async Task DeleteAsync(int id, ClaimsPrincipal userClaims)
        {
            await EnsureCanModifyCategoryAsync(id, userClaims);

            var inUse = await _categoryRepository.CountTransactionsAsync(id);
            if (inUse > 0)
                throw new ConflictException($"Categoria em uso por {inUse} transação(ões).");

            await _categoryRepository.DeleteAsync(id);
        }

        private static string? GetCallerId(ClaimsPrincipal userClaims)
        {
            var callerId = userClaims.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(callerId))
                throw new SecurityException("Você não tem permissão para acessar categorias.");

            return callerId;
        }

        // Administrator vê só as globais (nulo); User vê as globais e as dele
        private static string? GetVisibilityUserId(ClaimsPrincipal userClaims)
        {
            return userClaims.IsInRole("Administrator") ? null : GetCallerId(userClaims);
        }

        private async Task<CategoryAccessInfo> EnsureCanModifyCategoryAsync(int categoryId, ClaimsPrincipal userClaims)
        {
            var info = await _categoryRepository.GetAccessInfoAsync(categoryId);
            if (info == null)
                throw new NotFoundException("Categoria", categoryId);

            var canManageAny = userClaims.IsInRole("Administrator")
                || userClaims.HasClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageCategories));

            if (info.UserId == null)
            {
                if (!canManageAny)
                    throw new SecurityException("Você não tem permissão para alterar esta categoria.");
            }
            else if (!canManageAny && info.UserId != userClaims.FindFirst("uid")?.Value)
            {
                // Categoria pessoal de outro usuário: 404 para não revelar que existe
                throw new NotFoundException("Categoria", categoryId);
            }

            return info;
        }
    }
}
