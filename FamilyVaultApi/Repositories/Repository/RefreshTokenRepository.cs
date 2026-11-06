using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly DatabaseContext _context;

        public RefreshTokenRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RefreshToken token)
        {
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();
        }

        public async Task<RefreshToken?> FindByHashAsync(string tokenHash)
        {
            return await _context.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }

        public async Task<bool> TryConsumeAsync(long refreshTokenId, DateTime now)
        {
            var affected = await _context.RefreshTokens
                .Where(t => t.RefreshTokenId == refreshTokenId && t.ConsumedAt == null && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now));

            return affected == 1;
        }

        public async Task RevokeFamilyAsync(string familyId, DateTime now)
        {
            await _context.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
        }

        public async Task RevokeAllForUserAsync(string userId, DateTime now)
        {
            await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
        }

        public async Task DeleteExpiredForUserAsync(string userId, DateTime now)
        {
            await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.ExpiresAt < now)
                .ExecuteDeleteAsync();
        }
    }
}
