using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;


namespace FamilyVaultApi.Repositories.Repository
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;

        public TransactionRepository(DatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<TransactionResponseDto> AddAsync(CreateTransactionDto dto)
        {
            var entity = _mapper.Map<Transaction>(dto);
            entity.CreatedAt = DateTime.UtcNow;

            _context.Transactions.Add(entity);
            await _context.SaveChangesAsync();

            return await GetResponseByIdAsync(entity.TransactionId);
        }

        public async Task<PagedResult<TransactionResponseDto>> GetAllAsync(TransactionQueryRequestDto query)
        {
            var q = _context.Transactions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.UserId))
                q = q.Where(x => x.UserId == query.UserId);

            if (query.CategoryId.HasValue)
                q = q.Where(x => x.CategoryId == query.CategoryId);

            if (query.TransactionTypeId.HasValue)
                q = q.Where(x => x.TransactionTypeId == query.TransactionTypeId);

            if (!string.IsNullOrWhiteSpace(query.Description))
                q = q.Where(x => x.Description.Contains(query.Description));

            if (query.StartDate.HasValue)
            {
                var startDate = query.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                q = q.Where(x => x.TransactionDate >= startDate);
            }

            if (query.EndDate.HasValue)
            {
                var endDate = query.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                q = q.Where(x => x.TransactionDate < endDate);
            }

            if (query.MinAmount.HasValue)
                q = q.Where(x => x.Amount >= query.MinAmount.Value);

            if (query.MaxAmount.HasValue)
                q = q.Where(x => x.Amount <= query.MaxAmount.Value);

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(x => x.TransactionDate)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ProjectTo<TransactionResponseDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            return new PagedResult<TransactionResponseDto>
            {
                TotalCount = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = items
            };
        }

        public async Task<TransactionResponseDto> UpdateAsync(int id, UpdateTransactionDto dto)
        {
            var entity = await _context.Transactions.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("Transaction", id);

            if (!await _context.Categories.AnyAsync(x => x.CategoryId == dto.CategoryId))
                throw new NotFoundException("Category não encontrada", dto.CategoryId);

            if (!await _context.TransactionTypes.AnyAsync(x => x.TransactionTypeId == dto.TransactionTypeId))
                throw new NotFoundException("TransactionType não encontrado", dto.TransactionTypeId);

            entity.CategoryId = dto.CategoryId;
            entity.TransactionTypeId = dto.TransactionTypeId;
            entity.Description = dto.Description;
            entity.Amount = dto.Amount;
            entity.TransactionDate = dto.TransactionDate;

            await _context.SaveChangesAsync();

            return await GetResponseByIdAsync(entity.TransactionId);
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.Transactions.FindAsync(id);
            if (entity == null)
                throw new NotFoundException("Transaction", id);

            _context.Transactions.Remove(entity);
            await _context.SaveChangesAsync();
        }

        // Recarrega pelo id com a mesma projeção da listagem (Category e TransactionType preenchidos)
        private async Task<TransactionResponseDto> GetResponseByIdAsync(int transactionId)
        {
            return await _context.Transactions
                .Where(x => x.TransactionId == transactionId)
                .ProjectTo<TransactionResponseDto>(_mapper.ConfigurationProvider)
                .FirstAsync();
        }

        public async Task<bool> UserExistsAsync(string userId)
        {
            return await _context.Users.AnyAsync(x => x.Id == userId);
        }

        public async Task<string?> GetOwnerUserIdAsync(int transactionId)
        {
            return await _context.Transactions
                .Where(x => x.TransactionId == transactionId)
                .Select(x => x.UserId)
                .FirstOrDefaultAsync();
        }
    }
}
