using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Helpers
{
    public static class ClaimsPrincipalTestHelper
    {
        public static ClaimsPrincipal CreateAdmin(string uid = "admin-1") => Create(uid, "Administrator");

        public static ClaimsPrincipal CreateUser(string uid = "user-1") => Create(uid, "User");

        public static ClaimsPrincipal CreateUnauthenticated() => new(new ClaimsIdentity());

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
