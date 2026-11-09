using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Services.Service
{
    // Textos do código de verificação, compartilhados pelos senders de SMS e de e-mail
    public static class VerificationCodeTexts
    {
        public static string Body(VerificationPurpose purpose, string code) => purpose switch
        {
            VerificationPurpose.Registration => $"Family Vault: seu código de cadastro é {code}. Não compartilhe.",
            _ => $"Family Vault: seu código de recuperação é {code}. Não compartilhe."
        };

        public static string Subject(VerificationPurpose purpose) => purpose switch
        {
            VerificationPurpose.Registration => "Seu código de cadastro do Family Vault",
            _ => "Seu código de recuperação do Family Vault"
        };
    }
}
