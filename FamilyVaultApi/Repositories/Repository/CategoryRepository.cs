using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
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


        public async Task<CategoryResponseDto> AddAsync(CreateCategoryDto dto)
        {
            var purposeExists = await _context.CategoryPurposes
            .AnyAsync(x => x.CategoryPurposeId == dto.CategoryPurposeId);


            if (!purposeExists)
                throw new NotFoundException("CategoryPurpose não encontrado", purposeExists);


            var entity = _mapper.Map<Category>(dto);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;


            _context.Categories.Add(entity);
            await _context.SaveChangesAsync();


            return _mapper.Map<CategoryResponseDto>(entity);
        }


        public async Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query)
        {
            var q = _context.Categories.AsQueryable();


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


        public async Task DeleteAsync(int id)
        {
            var entity = await _context.Categories.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("Category", id);


            _context.Categories.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
