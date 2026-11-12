namespace FamilyVaultApi.Models.Internal
{
    // Dados do ID token do Google já validado
    public record GoogleIdentity(string Subject, string? Email, bool EmailVerified, string? GivenName, string? FamilyName, string? Name);
}
