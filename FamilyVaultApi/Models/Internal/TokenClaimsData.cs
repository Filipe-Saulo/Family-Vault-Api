using System.Security.Claims;

namespace FamilyVaultApi.Models.Internal
{
    public class TokenClaimsData
    {
        public string UserId { get; init; }
        public string Identifier { get; init; }
        public bool IsAdmin { get; init; }
        public bool IsUser { get; init; }
        public string SecurityStamp { get; init; }
        public IEnumerable<Claim> UserClaims { get; init; } = Enumerable.Empty<Claim>();
        public IEnumerable<Claim> PermissionClaims { get; init; } = Enumerable.Empty<Claim>();
    }
}
