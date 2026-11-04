namespace FamilyVaultApi.Models.Dto.Requests.Dashboard
{
    public class DashboardQueryRequestDto
    {
        public string? UserId { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }
}
