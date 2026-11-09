using FamilyVaultApi.Services.Service;

namespace FamilyVaultApi.Services.IService
{
    public interface IVerificationCodeSender
    {
        Task SendAsync(VerificationCodeMessage message);
    }
}
