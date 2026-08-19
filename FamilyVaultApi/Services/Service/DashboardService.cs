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

            return await _repository.GetSummaryAsync(query);
        }
    }
}
