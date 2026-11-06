using FamilyVaultApi.Data.Entities;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshToken token);
        Task<RefreshToken?> FindByHashAsync(string tokenHash);

        // Marca como consumido só se ninguém consumiu antes; true para quem ganhou a corrida
        Task<bool> TryConsumeAsync(long refreshTokenId, DateTime now);

        Task RevokeFamilyAsync(string familyId, DateTime now);
        Task RevokeAllForUserAsync(string userId, DateTime now);
        Task DeleteExpiredForUserAsync(string userId, DateTime now);
    }
}
