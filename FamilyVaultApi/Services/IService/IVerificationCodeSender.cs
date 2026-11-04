namespace FamilyVaultApi.Services.IService
{
    public interface IVerificationCodeSender
    {
        Task SendAsync(string phoneE164, string code);
    }
}
