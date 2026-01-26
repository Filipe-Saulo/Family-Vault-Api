namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class RefreshTokenRequestDto
    {
        public string Token { get; init; }
        public string RefreshToken { get; init; }
    }
}
