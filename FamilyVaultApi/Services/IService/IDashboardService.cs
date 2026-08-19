using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetSummaryAsync(DashboardQueryRequestDto query, ClaimsPrincipal userClaims);
    }
}
