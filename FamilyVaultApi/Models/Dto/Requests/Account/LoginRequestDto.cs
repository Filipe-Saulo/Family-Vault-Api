using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class LoginRequestDto
    {
        public string? Phone { get; init; }
        public string? Email { get; init; }
        [Required(ErrorMessage = "Password é obrigatório.")]
        public string Password { get; init; }               
    }
}
