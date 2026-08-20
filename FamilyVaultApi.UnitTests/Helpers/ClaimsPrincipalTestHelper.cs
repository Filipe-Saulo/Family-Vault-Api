using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Internal.Enums;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Helpers
{
    public static class ClaimsPrincipalTestHelper
    {
        public static ClaimsPrincipal CreateAdmin(string uid = "admin-1") => Create(uid, "Administrator");

        public static ClaimsPrincipal CreateUser(string uid = "user-1") => Create(uid, "User");

        public static ClaimsPrincipal CreateUnauthenticated() => new(new ClaimsIdentity());

        public static ClaimsPrincipal CreateUserWithPermission(string uid, PermissionCode permission)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("uid", uid),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(AppClaimTypes.Permission, permission.ToString())
            }, authenticationType: "TestAuth");

            return new ClaimsPrincipal(identity);
        }

        private static ClaimsPrincipal Create(string uid, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("uid", uid),
                new Claim(ClaimTypes.Role, role)
            }, authenticationType: "TestAuth");

            return new ClaimsPrincipal(identity);
        }
    }
}
