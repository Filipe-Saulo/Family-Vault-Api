using FamilyVaultApi.Data.Entities;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IAccountRepository
    {
        Task<bool> PhoneExistsAsync(string phoneNumber);
        Task<bool> EmailUserExistsAsync(string email);
    }
}
