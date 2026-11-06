using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private static readonly TimeSpan _lifetime = TimeSpan.FromDays(7);

        // Duas renovações em corrida ou uma resposta perdida na rede não são roubo de token
        private static readonly TimeSpan _reuseGrace = TimeSpan.FromSeconds(10);

        private readonly IRefreshTokenRepository _repository;

        public RefreshTokenService(IRefreshTokenRepository repository)
        {
            _repository = repository;
        }

        public async Task<string> CreateSessionAsync(string userId, AuthClient client)
        {
            await _repository.DeleteExpiredForUserAsync(userId, DateTime.UtcNow);

            return await IssueAsync(userId, client, Guid.NewGuid().ToString());
        }

        public async Task<string?> RotateAsync(string userId, AuthClient client, string refreshToken)
        {
            var stored = await _repository.FindByHashAsync(Hash(refreshToken));
            var now = DateTime.UtcNow;

            if (stored == null
                || stored.UserId != userId
                || stored.Client != client
                || stored.RevokedAt != null
                || stored.ExpiresAt <= now)
                return null;

            if (stored.ConsumedAt == null)
            {
                if (await _repository.TryConsumeAsync(stored.RefreshTokenId, now))
                    return await IssueAsync(userId, client, stored.FamilyId);

                // Perdeu a corrida: para outra renovação ou para um logout, que não pode ser desfeito aqui
                stored = await _repository.FindByHashAsync(stored.TokenHash);
                if (stored == null || stored.RevokedAt != null || stored.ConsumedAt == null)
                    return null;
            }

            if (now - stored.ConsumedAt.Value <= _reuseGrace)
                return await IssueAsync(userId, client, stored.FamilyId);

            // Token antigo voltou depois da janela: pode ter sido roubado, então a sessão inteira cai
            await _repository.RevokeFamilyAsync(stored.FamilyId, now);
            return null;
        }

        public async Task RevokeSessionAsync(string refreshToken)
        {
            var stored = await _repository.FindByHashAsync(Hash(refreshToken));
            if (stored != null)
                await _repository.RevokeFamilyAsync(stored.FamilyId, DateTime.UtcNow);
        }

        public async Task RevokeAllSessionsAsync(string userId)
        {
            await _repository.RevokeAllForUserAsync(userId, DateTime.UtcNow);
        }

        private async Task<string> IssueAsync(string userId, AuthClient client, string familyId)
        {
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var now = DateTime.UtcNow;

            await _repository.AddAsync(new RefreshToken
            {
                TokenHash = Hash(token),
                FamilyId = familyId,
                UserId = userId,
                Client = client,
                CreatedAt = now,
                ExpiresAt = now.Add(_lifetime),
            });

            return token;
        }

        // Só o hash vai para o banco: um vazamento da tabela não entrega tokens utilizáveis
        private static string Hash(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }
    }
}
