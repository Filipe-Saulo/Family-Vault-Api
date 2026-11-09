using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Repositories.Repository
{
    public class DbRegistrationCodeStore : IRegistrationCodeStore
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private const int MaxFailures = 5;

        private readonly DatabaseContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly byte[] _hashKey;

        public DbRegistrationCodeStore(DatabaseContext context, TimeProvider timeProvider, IConfiguration configuration)
        {
            _context = context;
            _timeProvider = timeProvider;
            // Só 1 milhão de códigos possíveis: sem a chave, quem vazar o banco descobre o código pelo hash
            _hashKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"]!),
                32,
                info: Encoding.UTF8.GetBytes("verification-code"));
        }

        public async Task<string> IssueAsync(string destination, VerificationPurpose purpose)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var hash = Convert.ToHexString(Hash(destination, purpose, code)).ToLowerInvariant();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = now.Add(Lifetime);

            if (await ReplaceAsync(destination, purpose, hash, expiresAt, now))
                return code;

            var entity = new VerificationCode
            {
                Destination = destination,
                Purpose = purpose,
                CodeHash = hash,
                ExpiresAt = expiresAt,
                CreatedAt = now
            };
            _context.VerificationCodes.Add(entity);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Outro pedido criou a linha ao mesmo tempo; se não foi isso, o código não foi gravado e não pode ser enviado
                _context.Entry(entity).State = EntityState.Detached;
                if (!await ReplaceAsync(destination, purpose, hash, expiresAt, now))
                    throw;
            }

            return code;
        }

        public async Task<bool> VerifyAsync(string destination, VerificationPurpose purpose, string code)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Reserva a tentativa antes de comparar: pedidos simultâneos não passam do limite de erros
            var reserved = await Pending(destination, purpose)
                .Where(c => c.ExpiresAt > now && c.FailedAttempts < MaxFailures)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts + 1));
            if (reserved == 0)
                return false;

            var stored = await Pending(destination, purpose).Select(c => c.CodeHash).FirstOrDefaultAsync();
            if (stored == null)
                return false;

            if (CryptographicOperations.FixedTimeEquals(Convert.FromHexString(stored), Hash(destination, purpose, code ?? string.Empty)))
            {
                await Pending(destination, purpose)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts - 1));
                return true;
            }

            await Pending(destination, purpose)
                .Where(c => c.FailedAttempts >= MaxFailures)
                .ExecuteDeleteAsync();
            return false;
        }

        public async Task RemoveAsync(string destination, VerificationPurpose purpose)
        {
            await Pending(destination, purpose).ExecuteDeleteAsync();
        }

        private IQueryable<VerificationCode> Pending(string destination, VerificationPurpose purpose) =>
            _context.VerificationCodes.Where(c => c.Destination == destination && c.Purpose == purpose);

        private async Task<bool> ReplaceAsync(string destination, VerificationPurpose purpose, string hash, DateTime expiresAt, DateTime now)
        {
            var rows = await Pending(destination, purpose).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.CodeHash, hash)
                .SetProperty(c => c.ExpiresAt, expiresAt)
                .SetProperty(c => c.FailedAttempts, 0)
                .SetProperty(c => c.CreatedAt, now));

            return rows > 0;
        }

        private byte[] Hash(string destination, VerificationPurpose purpose, string code) =>
            HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes($"{(int)purpose}:{destination}:{code}"));
    }
}
