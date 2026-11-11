namespace FamilyVaultApi.Models.Dto.Responses.McpToken
{
    public class McpTokenResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsExpired { get; set; }
    }

    // Resposta do POST: a única vez em que o valor do token aparece
    public class McpTokenCreatedResponseDto : McpTokenResponseDto
    {
        public string Token { get; set; } = string.Empty;
    }
}
