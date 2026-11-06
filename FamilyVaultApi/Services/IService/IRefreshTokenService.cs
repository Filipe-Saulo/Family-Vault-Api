using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Services.IService
{
    public interface IRefreshTokenService
    {
        // Abre uma sessão nova, sem afetar as outras do usuário
        Task<string> CreateSessionAsync(string userId, AuthClient client);

        // Troca o token pelo próximo da mesma sessão; null quando o token não vale mais
        Task<string?> RotateAsync(string userId, AuthClient client, string refreshToken);

        // Encerra só a sessão do token informado; token desconhecido é ignorado
        Task RevokeSessionAsync(string refreshToken);

        Task RevokeAllSessionsAsync(string userId);
    }
}
