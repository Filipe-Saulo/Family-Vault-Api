using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(TokenClaimsData data)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var identifierClaim = data.IsAdmin
                ? new Claim(JwtRegisteredClaimNames.Email, data.Identifier)
                : new Claim("phone_number", data.Identifier);

            var roleClaims = new List<Claim>();
            if (data.IsAdmin) roleClaims.Add(new Claim(ClaimTypes.Role, "Administrator"));
            if (data.IsUser) roleClaims.Add(new Claim(ClaimTypes.Role, "User"));

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, data.Identifier),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                identifierClaim,
                new Claim("uid", data.UserId),
                new Claim("SecurityStamp", data.SecurityStamp)
            }
            .Union(data.UserClaims)
            .Union(roleClaims)
            .Union(data.PermissionClaims);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:DurationInMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
