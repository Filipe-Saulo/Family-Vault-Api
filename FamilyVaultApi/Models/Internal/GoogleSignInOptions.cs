namespace FamilyVaultApi.Models.Internal
{
    // Client IDs OAuth aceitos no aud do ID token (web, que é o aud do SDK nativo, e iOS).
    // Em user-secrets ou GoogleSignIn__ClientIds__0...; nunca no appsettings.
    public class GoogleSignInOptions
    {
        public List<string> ClientIds { get; set; } = new();

        public bool IsConfigured => ClientIds.Any(id => !string.IsNullOrWhiteSpace(id));
    }
}
