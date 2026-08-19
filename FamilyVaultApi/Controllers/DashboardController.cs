using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService service)
        {
            _dashboardService = service;
        }

        [HttpGet("summary")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> GetSummary([FromQuery] DashboardQueryRequestDto query)
        {
            var result = await _dashboardService.GetSummaryAsync(query, User);
            return Ok(ApiResponse<DashboardSummaryDto>.Ok(result));
        }
    }
}
