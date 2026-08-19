namespace FamilyVaultApi.Models.Dto.Requests.Dashboard
{
    public class DashboardQueryRequestDto
    {
        public string? UserId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
