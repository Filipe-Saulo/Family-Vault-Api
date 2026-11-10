using FamilyVaultApi.Models.Dto.Requests.Gmail;
using FamilyVaultApi.Models.Dto.Responses.Gmail;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IGmailConnectionService
    {
        Task<GmailConnectResponseDto> ConnectAsync(GmailConnectRequestDto? dto, ClaimsPrincipal userClaims);
        // Nunca lança: devolve a URL de volta ao app, com ?gmail=connected ou ?gmail=error&reason=...
        Task<string> HandleCallbackAsync(string? code, string? state, string? error, CancellationToken cancellationToken);
        Task<GmailStatusResponseDto> GetStatusAsync(ClaimsPrincipal userClaims);
        Task DisconnectAsync(ClaimsPrincipal userClaims, CancellationToken cancellationToken);
        Task<GmailScanCreatedResponseDto> StartScanAsync(ClaimsPrincipal userClaims);
        Task<GmailScanResponseDto> GetScanAsync(int scanId, ClaimsPrincipal userClaims);
        Task<List<GmailIgnoredSenderResponseDto>> ListIgnoredSendersAsync(ClaimsPrincipal userClaims);
        Task DeleteIgnoredSenderAsync(int id, ClaimsPrincipal userClaims);
    }
}
