using FamilyVaultApi.Data.Entities;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IAccountRepository
    {
        Task<User?> FindByPhoneAsync(string phone);
        Task UpdatePasswordAsync(User user, string newPassword);
        Task<bool> PhoneExistsAsync(string phoneNumber);
        Task<bool> EmailUserExistsAsync(string email);
    }
}
