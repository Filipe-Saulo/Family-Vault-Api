using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FamilyVaultApi.UnitTests.Helpers
{
    public static class JwtTokenTestHelper
    {
        private static readonly SymmetricSecurityKey SigningKey =
            new(Encoding.UTF8.GetBytes("unit-test-signing-key-not-used-in-production-123456"));

        public static string CreateToken(
            string? uid = "user-1",
            bool includeUsernameClaim = true,
            string usernameClaimType = JwtRegisteredClaimNames.Email,
            string usernameClaimValue = "user@example.com")
        {
            var claims = new List<Claim>();
            if (uid is not null)
                claims.Add(new Claim("uid", uid));

            if (includeUsernameClaim)
                claims.Add(new Claim(usernameClaimType, usernameClaimValue));

            var token = new JwtSecurityToken(
                claims: claims,
                signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
