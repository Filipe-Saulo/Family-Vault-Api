using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class CategoryPurposeRepository : ICategoryPurposeRepository
    {
        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;

        public CategoryPurposeRepository(DatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<CategoryPurposeResponseDto> AddAsync(CreateCategoryPurposeDto dto)
        {
            var code = dto.Code.ToString().ToLowerInvariant();

            var codeExists = await _context.CategoryPurposes.AnyAsync(x => x.Code == code);
            if (codeExists)
                throw new BadRequestException($"Já existe um CategoryPurpose com o code '{code}'.");

            var entity = _mapper.Map<CategoryPurpose>(dto);
            entity.Code = code;
            entity.IsActive = true;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            _context.CategoryPurposes.Add(entity);
            await _context.SaveChangesAsync();

            return _mapper.Map<CategoryPurposeResponseDto>(entity);
        }

        public async Task<List<CategoryPurposeResponseDto>> GetAllAsync(bool? isActive)
        {
            var q = _context.CategoryPurposes.AsQueryable();

            if (isActive.HasValue)
                q = q.Where(x => x.IsActive == isActive.Value);

            return await q
                .OrderBy(x => x.Name)
                .ProjectTo<CategoryPurposeResponseDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<CategoryPurposeResponseDto> UpdateAsync(int id, UpdateCategoryPurposeDto dto)
        {
            var entity = await _context.CategoryPurposes.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("CategoryPurpose", id);

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return _mapper.Map<CategoryPurposeResponseDto>(entity);
        }
    }
}
