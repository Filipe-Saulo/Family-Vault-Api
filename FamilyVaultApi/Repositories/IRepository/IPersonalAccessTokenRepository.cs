using FamilyVaultApi.Data.Entities;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IPersonalAccessTokenRepository
    {
        Task AddAsync(PersonalAccessToken token);
        Task<PersonalAccessToken?> FindByHashAsync(string tokenHash);
        Task<PersonalAccessToken?> FindByIdAsync(int id);

        // Não revogados, vencidos inclusive, mais novos primeiro
        Task<List<PersonalAccessToken>> GetNotRevokedForUserAsync(string userId);
        Task<int> CountActiveForUserAsync(string userId, DateTime now);

        // Só marca se ainda não foi revogado; true para quem revogou
        Task<bool> RevokeAsync(int id, DateTime now);
        Task UpdateLastUsedAsync(int id, DateTime now);
    }
}
