namespace FamilyVaultApi.Models.Internal
{
    public class AuthResult
    {
        public string UserId { get; init; }
        public string Token { get; init; }
        public string RefreshToken { get; init; }        
        public bool IsAdmin { get; init; }
        public bool IsUser { get; init; }
    }
}
