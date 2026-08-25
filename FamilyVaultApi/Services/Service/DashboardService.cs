using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _repository;

        public DashboardService(IDashboardRepository repository)
        {
            _repository = repository;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardQueryRequestDto query, ClaimsPrincipal userClaims)
        {
            query ??= new DashboardQueryRequestDto();

            var isAdmin = userClaims.IsInRole("Administrator");
            if (!isAdmin)
            {
                query.UserId = userClaims.FindFirst("uid")?.Value;
            }

            if (!query.StartDate.HasValue && !query.EndDate.HasValue)
            {
                var (startOfMonth, endOfMonth) = GetCurrentMonthRange();
                query.StartDate = startOfMonth;
                query.EndDate = endOfMonth;
            }

            return await _repository.GetSummaryAsync(query);
        }

        private static (DateTime Start, DateTime End) GetCurrentMonthRange()
        {
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddTicks(-1);
            return (start, end);
        }
    }
}
