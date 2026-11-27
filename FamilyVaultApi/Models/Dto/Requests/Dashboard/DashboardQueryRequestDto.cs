using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FamilyVaultApi.Models.Dto.Requests.Dashboard
{
    public class DashboardQueryRequestDto
    {
        public string? UserId { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        // Preenchido pelo service para a conta demo; não vem da query string
        [BindNever]
        public bool DemoOnly { get; set; }
    }
}
