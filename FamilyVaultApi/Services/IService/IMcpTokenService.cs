using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.McpToken;
using FamilyVaultApi.Models.Dto.Responses.McpToken;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IMcpTokenService
    {
        Task<List<McpTokenResponseDto>> GetMineAsync(ClaimsPrincipal userClaims);
        Task<McpTokenCreatedResponseDto> CreateAsync(CreateMcpTokenDto dto, ClaimsPrincipal userClaims);
        Task RevokeAsync(int id, ClaimsPrincipal userClaims);

        // Token válido (existe, não revogado, não vencido) ou null. Usado pelo esquema de autenticação do /mcp.
        Task<PersonalAccessToken?> ValidateAsync(string rawToken);
    }
}
