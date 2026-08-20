using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class AccountRepository : IAccountRepository
    {
        private readonly DatabaseContext _context;

        public AccountRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<User?> FindByPhoneAsync(string phone)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
        }

        public async Task UpdatePasswordAsync(User user, string newPassword)
        {
            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.UserName = user.PhoneNumber;
            user.NormalizedUserName = user.PhoneNumber.ToUpper();

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> PhoneExistsAsync(string phoneNumber)
        {
            return await _context.Users
                .AnyAsync(u => u.PhoneNumber != null && u.PhoneNumber == phoneNumber);
        }

        public async Task<bool> EmailUserExistsAsync(string email)
        {
            return await _context.Users
                .AnyAsync(u => u.Email == email);
        }
    }
}
