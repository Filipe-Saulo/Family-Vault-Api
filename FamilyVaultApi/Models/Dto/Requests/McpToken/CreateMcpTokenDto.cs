using FamilyVaultApi.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.McpToken
{
    public class CreateMcpTokenDto
    {
        [Required(ErrorMessage = "O nome do token é obrigatório.")]
        [MaxLength(PersonalAccessToken.MaxNameLength, ErrorMessage = "O nome do token deve ter no máximo 100 caracteres.")]
        public string Name { get; set; } = string.Empty;

        // 30, 90 ou 365; sem o campo, 90
        public int? ExpiresInDays { get; set; }
    }
}
