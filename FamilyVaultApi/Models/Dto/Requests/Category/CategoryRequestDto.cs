namespace FamilyVaultApi.Models.Dto.Requests.Category
{
    public class CategoryQueryRequestDto : PagedFilterDto
    {
        public string? Description { get; set; }
        public int? CategoryPurposeId { get; set; }
    }
}
