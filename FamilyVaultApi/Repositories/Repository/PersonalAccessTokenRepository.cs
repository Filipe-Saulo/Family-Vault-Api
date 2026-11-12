using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class PersonalAccessTokenRepository : IPersonalAccessTokenRepository
    {
        private readonly DatabaseContext _context;

        public PersonalAccessTokenRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task AddAsync(PersonalAccessToken token)
        {
            _context.PersonalAccessTokens.Add(token);
            await _context.SaveChangesAsync();
        }

        public async Task<PersonalAccessToken?> FindByHashAsync(string tokenHash)
        {
            return await _context.PersonalAccessTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public async Task<PersonalAccessToken?> FindByIdAsync(int id)
        {
            return await _context.PersonalAccessTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.PersonalAccessTokenId == id);
        }

        public async Task<List<PersonalAccessToken>> GetNotRevokedForUserAsync(string userId)
        {
            return await _context.PersonalAccessTokens
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.PersonalAccessTokenId)
                .ToListAsync();
        }

        public async Task<int> CountActiveForUserAsync(string userId, DateTime now)
        {
            return await _context.PersonalAccessTokens
                .CountAsync(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now);
        }

        public async Task<bool> RevokeAsync(int id, DateTime now)
        {
            var affected = await _context.PersonalAccessTokens
                .Where(t => t.PersonalAccessTokenId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));

            return affected == 1;
        }

        public async Task UpdateLastUsedAsync(int id, DateTime now)
        {
            await _context.PersonalAccessTokens
                .Where(t => t.PersonalAccessTokenId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, now));
        }
    }
}
