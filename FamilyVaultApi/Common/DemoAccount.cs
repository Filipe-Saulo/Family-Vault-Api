using System.Security.Claims;

namespace FamilyVaultApi.Common
{
    public static class DemoAccount
    {
        public const string ClaimValue = "true";

        // A claim entra no JWT quando o usuário tem is_demo (AccountService.BuildAccessTokenAsync)
        public static bool IsDemo(this ClaimsPrincipal? principal) =>
            principal?.HasClaim(AppClaimTypes.Demo, ClaimValue) == true;
    }
}
