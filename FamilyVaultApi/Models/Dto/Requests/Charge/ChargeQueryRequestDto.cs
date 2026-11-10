using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Requests.Charge
{
    public class ChargeQueryRequestDto : PagedFilterDto
    {
        public ChargeStatus? Status { get; set; }
    }
}
