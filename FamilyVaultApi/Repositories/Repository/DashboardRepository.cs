using FamilyVaultApi.Data;
using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly DatabaseContext _context;

        public DashboardRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardQueryRequestDto query)
        {
            var q = _context.Transactions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.UserId))
                q = q.Where(x => x.UserId == query.UserId);

            if (query.StartDate.HasValue)
                q = q.Where(x => x.TransactionDate >= query.StartDate.Value);

            if (query.EndDate.HasValue)
                q = q.Where(x => x.TransactionDate <= query.EndDate.Value);

            var expenseCode = TransactionTypeCode.Expense.ToString().ToLowerInvariant();
            var incomeCode = TransactionTypeCode.Income.ToString().ToLowerInvariant();

            var totalExpense = await q
                .Where(x => x.TransactionType.Code == expenseCode)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            var totalIncome = await q
                .Where(x => x.TransactionType.Code == incomeCode)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            var rawByCategory = await q
                .GroupBy(x => new { x.CategoryId, x.Category.Description, x.TransactionTypeId, x.TransactionType.Code, x.TransactionType.Name })
                .Select(g => new
                {
                    g.Key.CategoryId,
                    g.Key.Description,
                    g.Key.TransactionTypeId,
                    g.Key.Code,
                    g.Key.Name,
                    Total = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            var byCategory = rawByCategory
                .Select(x => new CategorySummaryItemDto
                {
                    CategoryId = x.CategoryId,
                    CategoryDescription = x.Description,
                    TransactionTypeId = x.TransactionTypeId,
                    TransactionTypeCode = Enum.Parse<TransactionTypeCode>(x.Code, true),
                    TransactionTypeName = x.Name,
                    Total = x.Total
                })
                .ToList();

            return new DashboardSummaryDto
            {
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                Balance = totalIncome - totalExpense,
                ByCategory = byCategory
            };
        }
    }
}
