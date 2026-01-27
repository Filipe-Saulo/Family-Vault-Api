namespace FamilyVaultApi.Models.Dto.Requests.Transaction
{
    public class TransactionQueryRequestDto : PagedFilterDto
    {
        public string? UserId { get; set; }
        public int? CategoryId { get; set; }
        public int? TransactionTypeId { get; set; }


        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }


        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
    }
}
