namespace FamilyVaultApi.Models.Internal.Enums
{
    public enum InboundDocumentStatus
    {
        Pending,
        Processing,
        Done,
        Failed,
        // PDF com senha: espera a pessoa informar a senha ou o valor
        PasswordRequired
    }
}
