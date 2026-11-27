using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class UserQueryRequestDto : PagedFilterDto
    {
        public string? FirstName { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Search { get; set; }

        // Preenchido pelo service para a conta demo; não vem da query string
        [BindNever]
        public bool DemoOnly { get; set; }
    }
}
