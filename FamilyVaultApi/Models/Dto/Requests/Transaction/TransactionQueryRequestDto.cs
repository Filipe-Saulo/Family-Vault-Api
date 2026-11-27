using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FamilyVaultApi.Models.Dto.Requests.Transaction
{
    public class TransactionQueryRequestDto : PagedFilterDto
    {
        public string? UserId { get; set; }
        public int? CategoryId { get; set; }
        public int? TransactionTypeId { get; set; }
        public string? Description { get; set; }


        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }


        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }

        // Preenchido pelo service para a conta demo; não vem da query string
        [BindNever]
        public bool DemoOnly { get; set; }
    }
}
