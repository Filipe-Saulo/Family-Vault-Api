namespace FamilyVaultApi.Models.Dto.Responses.Dashboard
{
    public class DashboardSummaryDto
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }
        public List<CategorySummaryItemDto> ByCategory { get; set; } = new();
    }
}
