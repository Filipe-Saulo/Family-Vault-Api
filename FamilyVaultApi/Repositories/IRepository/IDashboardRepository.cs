using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IDashboardRepository
    {
        Task<DashboardSummaryDto> GetSummaryAsync(DashboardQueryRequestDto query);
    }
}
