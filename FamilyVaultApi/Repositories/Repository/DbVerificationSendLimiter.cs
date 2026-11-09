using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.Repositories.Repository
{
    public class DbVerificationSendLimiter : IVerificationSendLimiter
    {
        public const string GlobalSmsKey = "global:sms";

        private const string BrazilPrefix = "+55";
        private static readonly TimeSpan Day = TimeSpan.FromHours(24);

        private readonly DatabaseContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly VerificationSendLimitOptions _options;

        public DbVerificationSendLimiter(DatabaseContext context, TimeProvider timeProvider, IOptions<VerificationSendLimitOptions> options)
        {
            _context = context;
            _timeProvider = timeProvider;
            _options = options.Value;
        }

        public async Task<bool> TryAcquireAsync(VerificationChannel channel, string destination)
        {
            // Protege contra SMS pumping: destinos caros de fora e rajada para muitos números novos
            if (channel == VerificationChannel.Sms && !destination.StartsWith(BrazilPrefix, StringComparison.Ordinal))
                return false;

            // Só confere, sem consumir: com o teto atingido, o pedido não pode gastar a cota do destino
            if (channel == VerificationChannel.Sms && await IsGlobalSmsCapReachedAsync())
                return false;

            if (!await TryConsumeAsync(destination, TimeSpan.FromSeconds(_options.MinIntervalSeconds), _options.PerDay))
                return false;

            return channel != VerificationChannel.Sms || await TryConsumeAsync(GlobalSmsKey, TimeSpan.Zero, _options.GlobalSmsPerDay);
        }

        private async Task<bool> IsGlobalSmsCapReachedAsync()
        {
            var dayStart = _timeProvider.GetUtcNow().UtcDateTime - Day;

            return await _context.VerificationSendLimits.AnyAsync(l =>
                l.Destination == GlobalSmsKey && l.DayWindowStartedAt > dayStart && l.DayCount >= _options.GlobalSmsPerDay);
        }

        private async Task<bool> TryConsumeAsync(string key, TimeSpan minInterval, int perDay)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (await ConsumeExistingAsync(key, now, minInterval, perDay))
                return true;

            if (await _context.VerificationSendLimits.AnyAsync(l => l.Destination == key))
                return false;

            var entity = new VerificationSendLimit { Destination = key, LastSentAt = now, DayWindowStartedAt = now, DayCount = 1 };
            _context.VerificationSendLimits.Add(entity);

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // Outro pedido criou a linha ao mesmo tempo: vale o limite sobre ela
                _context.Entry(entity).State = EntityState.Detached;
                return await ConsumeExistingAsync(key, now, minInterval, perDay);
            }
        }

        private async Task<bool> ConsumeExistingAsync(string key, DateTime now, TimeSpan minInterval, int perDay)
        {
            var intervalStart = now - minInterval;
            var dayStart = now - Day;

            // Dois UPDATEs atômicos, de propósito: num só, o MySQL enxerga no CASE a coluna já alterada no mesmo SET
            await _context.VerificationSendLimits
                .Where(l => l.Destination == key && l.DayWindowStartedAt <= dayStart)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.DayCount, 0)
                    .SetProperty(l => l.DayWindowStartedAt, now));

            var rows = await _context.VerificationSendLimits
                .Where(l => l.Destination == key && l.LastSentAt <= intervalStart && l.DayCount < perDay)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.DayCount, l => l.DayCount + 1)
                    .SetProperty(l => l.LastSentAt, now));

            return rows > 0;
        }
    }
}
