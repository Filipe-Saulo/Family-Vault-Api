namespace FamilyVaultApi.Models.Internal.Enums
{
    public enum GmailConnectionStatus
    {
        Active,
        // O Google recusou o refresh token (invalid_grant): a pessoa precisa reconectar
        Revoked
    }
}
