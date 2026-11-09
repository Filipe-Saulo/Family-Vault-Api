using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FamilyVaultApi.UnitTests.Services
{
    public class TokenServiceTests
    {
        private const string Key = "unit-test-jwt-key-with-at-least-64-bytes-so-HS512-can-sign-it-0123456789-abcdef";
        private const string OtherKey = "another-unit-test-key-with-at-least-32-bytes-9876543210";
        private const string Issuer = "family-vault-tests";
        private const string Audience = "family-vault-clients";

        private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = Key,
                ["JwtSettings:Issuer"] = Issuer,
                ["JwtSettings:Audience"] = Audience,
                ["JwtSettings:DurationInMinutes"] = "15"
            })
            .Build();

        private static TokenService CreateService() => new(BuildConfiguration());

        private static string CreateRawToken(
            string key = Key,
            string issuer = Issuer,
            string audience = Audience,
            string algorithm = SecurityAlgorithms.HmacSha256,
            DateTime? expires = null)
        {
            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm);
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: new[] { new Claim("uid", "user-1") },
                notBefore: now.AddHours(-3),
                expires: expires ?? now.AddMinutes(10),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [Theory]
        [InlineData("fulano@exemplo.com", false, "email")]
        [InlineData("+5511987654312", false, "phone_number")]
        [InlineData("admin@exemplo.com", true, "email")]
        public void GenerateAccessToken_ShouldPutIdentifierInTheClaimMatchingItsKind(string identifier, bool isAdmin, string expectedClaim)
        {
            var service = CreateService();

            var token = service.GenerateAccessToken(new TokenClaimsData
            {
                UserId = "user-1",
                Identifier = identifier,
                IsAdmin = isAdmin,
                IsUser = !isAdmin,
                SecurityStamp = "stamp"
            });

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            jwt.Claims.Should().Contain(c => c.Type == expectedClaim && c.Value == identifier);
            var other = expectedClaim == "email" ? "phone_number" : "email";
            jwt.Claims.Should().NotContain(c => c.Type == other);
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenTokenIsValid_ShouldReturnPrincipalWithUid()
        {
            var service = CreateService();
            var token = service.GenerateAccessToken(new TokenClaimsData
            {
                UserId = "user-1",
                Identifier = "user@example.com",
                IsAdmin = true,
                SecurityStamp = "stamp"
            });

            var principal = service.GetPrincipalFromExpiredToken(token);

            principal.Should().NotBeNull();
            principal!.FindFirst("uid")!.Value.Should().Be("user-1");
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenTokenIsExpired_ShouldStillReturnPrincipalWithUid()
        {
            var token = CreateRawToken(expires: DateTime.UtcNow.AddHours(-1));

            var principal = CreateService().GetPrincipalFromExpiredToken(token);

            principal.Should().NotBeNull();
            principal!.FindFirst("uid")!.Value.Should().Be("user-1");
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenSignedWithWrongKey_ShouldReturnNull()
        {
            var token = CreateRawToken(key: OtherKey);

            CreateService().GetPrincipalFromExpiredToken(token).Should().BeNull();
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenIssuerIsWrong_ShouldReturnNull()
        {
            var token = CreateRawToken(issuer: "outro-issuer");

            CreateService().GetPrincipalFromExpiredToken(token).Should().BeNull();
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenAudienceIsWrong_ShouldReturnNull()
        {
            var token = CreateRawToken(audience: "outra-audience");

            CreateService().GetPrincipalFromExpiredToken(token).Should().BeNull();
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenAlgorithmIsNotHmacSha256_ShouldReturnNull()
        {
            var token = CreateRawToken(algorithm: SecurityAlgorithms.HmacSha512);

            CreateService().GetPrincipalFromExpiredToken(token).Should().BeNull();
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_WhenTokenIsMalformed_ShouldReturnNullWithoutThrowing()
        {
            Func<ClaimsPrincipal?> act = () => CreateService().GetPrincipalFromExpiredToken("isto-nao-e-um-jwt");

            act.Should().NotThrow();
            act().Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void GetPrincipalFromExpiredToken_WhenTokenIsNullOrEmpty_ShouldReturnNullWithoutThrowing(string? token)
        {
            Func<ClaimsPrincipal?> act = () => CreateService().GetPrincipalFromExpiredToken(token!);

            act.Should().NotThrow();
            act().Should().BeNull();
        }
    }
}
