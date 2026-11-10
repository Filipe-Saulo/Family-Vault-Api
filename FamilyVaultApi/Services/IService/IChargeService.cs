using FamilyVaultApi.Models.Dto;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IChargeService
    {
        Task<ChargeUploadResponseDto> UploadAsync(byte[] content, ClaimsPrincipal userClaims);
        Task<PagedResult<ChargeResponseDto>> GetAllAsync(ChargeQueryRequestDto query, ClaimsPrincipal userClaims);
        Task<ChargeResponseDto> GetByIdAsync(int id, ClaimsPrincipal userClaims);
        Task<PagedResult<ChargeFailureResponseDto>> GetFailuresAsync(PagedFilterDto query, ClaimsPrincipal userClaims);
        Task<TransactionResponseDto> ConfirmAsync(int id, ConfirmChargeDto dto, ClaimsPrincipal userClaims);
        Task DismissAsync(int id, DismissChargeDto? dto, ClaimsPrincipal userClaims);
    }
}
