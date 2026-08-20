using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class TransactionTypeRepository : ITransactionTypeRepository
    {
        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;

        public TransactionTypeRepository(DatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<TransactionTypeResponseDto> AddAsync(CreateTransactionTypeDto dto)
        {
            var code = dto.Code.ToString().ToLowerInvariant();

            var codeExists = await _context.TransactionTypes.AnyAsync(x => x.Code == code);
            if (codeExists)
                throw new BadRequestException($"Já existe um TransactionType com o code '{code}'.");

            var entity = _mapper.Map<TransactionType>(dto);
            entity.Code = code;
            entity.IsActive = true;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            _context.TransactionTypes.Add(entity);
            await _context.SaveChangesAsync();

            return _mapper.Map<TransactionTypeResponseDto>(entity);
        }

        public async Task<List<TransactionTypeResponseDto>> GetAllAsync(bool? isActive)
        {
            var q = _context.TransactionTypes.AsQueryable();

            if (isActive.HasValue)
                q = q.Where(x => x.IsActive == isActive.Value);

            return await q
                .OrderBy(x => x.Name)
                .ProjectTo<TransactionTypeResponseDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<TransactionTypeResponseDto> UpdateAsync(int id, UpdateTransactionTypeDto dto)
        {
            var entity = await _context.TransactionTypes.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("TransactionType", id);

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return _mapper.Map<TransactionTypeResponseDto>(entity);
        }

        public async Task<TransactionTypeCode?> GetCodeAsync(int transactionTypeId)
        {
            var code = await _context.TransactionTypes
                .Where(x => x.TransactionTypeId == transactionTypeId)
                .Select(x => x.Code)
                .FirstOrDefaultAsync();

            if (code == null)
                return null;

            return Enum.Parse<TransactionTypeCode>(code, true);
        }
    }
}
