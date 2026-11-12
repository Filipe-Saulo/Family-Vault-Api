namespace FamilyVaultApi.Data.Entities
{
    // Token pessoal do servidor MCP (fv_pat_...). Só o hash fica guardado; o valor aparece uma vez, na criação.
    public class PersonalAccessToken
    {
        public const int MaxNameLength = 100;

        public int PersonalAccessTokenId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TokenHash { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
