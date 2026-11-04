using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;


        public CategoryRepository(DatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }


        public async Task<CategoryResponseDto> AddAsync(CreateCategoryDto dto, string? userId)
        {
            var purposeExists = await _context.CategoryPurposes
            .AnyAsync(x => x.CategoryPurposeId == dto.CategoryPurposeId);


            if (!purposeExists)
                throw new NotFoundException("CategoryPurpose não encontrado", dto.CategoryPurposeId);


            var entity = _mapper.Map<Category>(dto);
            entity.UserId = userId;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;


            _context.Categories.Add(entity);
            await _context.SaveChangesAsync();


            return _mapper.Map<CategoryResponseDto>(entity);
        }


        public async Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query, string? ownerUserId)
        {
            var q = VisibleCategories(ownerUserId);


            if (!string.IsNullOrWhiteSpace(query.Description))
                q = q.Where(x => x.Description.Contains(query.Description));


            if (query.CategoryPurposeId.HasValue)
                q = q.Where(x => x.CategoryPurposeId == query.CategoryPurposeId);


            var total = await q.CountAsync();


            var items = await q
            .OrderBy(x => x.Description)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ProjectTo<CategoryResponseDto>(_mapper.ConfigurationProvider)
            .ToListAsync();


            return new PagedResult<CategoryResponseDto>
            {
                TotalCount = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = items
            };
        }


        public async Task<List<CategoryResponseDto>> GetLookupAsync(string? ownerUserId)
        {
            return await VisibleCategories(ownerUserId)
                .OrderBy(x => x.Description)
                .ProjectTo<CategoryResponseDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<CategoryResponseDto> UpdateAsync(int id, UpdateCategoryDto dto)
        {
            var entity = await _context.Categories.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("Category", id);

            var purposeExists = await _context.CategoryPurposes
                .AnyAsync(x => x.CategoryPurposeId == dto.CategoryPurposeId);

            if (!purposeExists)
                throw new NotFoundException("CategoryPurpose não encontrado", dto.CategoryPurposeId);

            entity.Description = dto.Description;
            entity.CategoryPurposeId = dto.CategoryPurposeId;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return _mapper.Map<CategoryResponseDto>(entity);
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.Categories.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("Category", id);


            _context.Categories.Remove(entity);
            await _context.SaveChangesAsync();
        }

        // ownerUserId nulo: só as globais; preenchido: as globais e as desse usuário
        private IQueryable<Category> VisibleCategories(string? ownerUserId)
        {
            if (ownerUserId == null)
                return _context.Categories.Where(x => x.UserId == null);

            return _context.Categories.Where(x => x.UserId == null || x.UserId == ownerUserId);
        }

        public async Task<CategoryAccessInfo?> GetAccessInfoAsync(int categoryId)
        {
            return await _context.Categories
                .Where(x => x.CategoryId == categoryId)
                .Select(x => new CategoryAccessInfo { UserId = x.UserId, CategoryPurposeId = x.CategoryPurposeId })
                .FirstOrDefaultAsync();
        }

        public async Task<int> CountTransactionsAsync(int categoryId)
        {
            return await _context.Transactions.CountAsync(x => x.CategoryId == categoryId);
        }

        public async Task<bool> IsAvailableToUserAsync(int categoryId, string userId)
        {
            return await _context.Categories
                .AnyAsync(x => x.CategoryId == categoryId && (x.UserId == null || x.UserId == userId));
        }

        public async Task<CategoryPurposeCode?> GetPurposeCodeAsync(int categoryId)
        {
            var code = await _context.Categories
                .Where(x => x.CategoryId == categoryId)
                .Select(x => x.Purpose.Code)
                .FirstOrDefaultAsync();

            if (code == null)
                return null;

            return Enum.Parse<CategoryPurposeCode>(code, true);
        }
    }
}
