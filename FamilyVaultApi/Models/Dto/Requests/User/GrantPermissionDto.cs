using System.Text.Json.Serialization;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class GrantPermissionDto
    {
        // aceita o número (4) ou o nome ("ManageUsers")
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PermissionCode Permission { get; set; }
    }
}
