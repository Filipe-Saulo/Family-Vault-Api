namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class UserQueryRequestDto : PagedFilterDto
    {
        public string? FirstName { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }
}
