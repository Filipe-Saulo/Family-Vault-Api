using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.McpToken;
using FamilyVaultApi.Models.Dto.Responses.McpToken;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.WebUtilities;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    public class McpTokenService : IMcpTokenService
    {
        public const string TokenPrefix = "fv_pat_";
        public const string ReadScope = "mcp:read";
        public const int DefaultExpiresInDays = 90;
        public const int MaxActiveTokens = 5;
        public static readonly IReadOnlyList<int> AllowedExpiresInDays = [30, 90, 365];

        // Grava o último uso no máximo uma vez por minuto, para não escrever no banco a cada chamada
        private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

        private readonly IPersonalAccessTokenRepository _repository;
        private readonly TimeProvider _timeProvider;

        public McpTokenService(IPersonalAccessTokenRepository repository, TimeProvider timeProvider)
        {
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public async Task<List<McpTokenResponseDto>> GetMineAsync(ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);
            var now = Now();

            var tokens = await _repository.GetNotRevokedForUserAsync(userId);
            return tokens.Select(t => ToResponse(t, now)).ToList();
        }

        public async Task<McpTokenCreatedResponseDto> CreateAsync(CreateMcpTokenDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var userId = GetCallerId(userClaims);

            var name = dto.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
                throw new BadRequestException("O nome do token é obrigatório.");

            var days = dto.ExpiresInDays ?? DefaultExpiresInDays;
            if (!AllowedExpiresInDays.Contains(days))
                throw new BadRequestException("Validade inválida: use 30, 90 ou 365 dias.");

            // Contar e depois inserir, sem trava: duas criações simultâneas da mesma pessoa podem passar de 5. Aceito,
            // porque só afeta quem criou e cada token continua revogável.
            var now = Now();
            if (await _repository.CountActiveForUserAsync(userId, now) >= MaxActiveTokens)
                throw new ConflictException($"Limite de {MaxActiveTokens} tokens ativos atingido. Revogue um para criar outro.");

            var rawToken = TokenPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var token = new PersonalAccessToken
            {
                UserId = userId,
                Name = name,
                TokenHash = Hash(rawToken),
                Scope = ReadScope,
                CreatedAt = now,
                ExpiresAt = now.AddDays(days)
            };

            await _repository.AddAsync(token);

            var response = ToResponse(token, now);
            return new McpTokenCreatedResponseDto
            {
                Id = response.Id,
                Name = response.Name,
                CreatedAt = response.CreatedAt,
                ExpiresAt = response.ExpiresAt,
                LastUsedAt = response.LastUsedAt,
                IsExpired = response.IsExpired,
                Token = rawToken
            };
        }

        public async Task RevokeAsync(int id, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            // Token de outra pessoa responde como inexistente, para não revelar que o id existe
            var token = await _repository.FindByIdAsync(id);
            if (token == null || token.UserId != userId)
                throw new NotFoundException("Token MCP", id);

            if (token.RevokedAt == null)
                await _repository.RevokeAsync(id, Now());
        }

        public async Task<PersonalAccessToken?> ValidateAsync(string rawToken)
        {
            if (string.IsNullOrEmpty(rawToken) || !rawToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
                return null;

            var token = await _repository.FindByHashAsync(Hash(rawToken));
            var now = Now();
            if (token == null || token.RevokedAt != null || token.ExpiresAt <= now || token.Scope != ReadScope)
                return null;

            if (token.LastUsedAt == null || now - token.LastUsedAt.Value >= LastUsedResolution)
                await _repository.UpdateLastUsedAsync(token.PersonalAccessTokenId, now);

            return token;
        }

        private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

        private static McpTokenResponseDto ToResponse(PersonalAccessToken token, DateTime now) => new()
        {
            Id = token.PersonalAccessTokenId,
            Name = token.Name,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            LastUsedAt = token.LastUsedAt,
            IsExpired = token.ExpiresAt <= now
        };

        private static string GetCallerId(ClaimsPrincipal userClaims)
        {
            var callerId = userClaims.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(callerId))
                throw new SecurityException("Você não tem permissão para gerenciar tokens do MCP.");

            return callerId;
        }

        private static string Hash(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }
    }
}
