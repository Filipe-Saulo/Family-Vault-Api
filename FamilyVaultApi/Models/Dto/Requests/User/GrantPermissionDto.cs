using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class GrantPermissionDto
    {
        public PermissionCode Permission { get; set; }
    }
}
