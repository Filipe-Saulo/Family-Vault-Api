using FamilyVaultApi.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    // A expiração vale pelo expires_at, na validação; aqui só se evita que as tabelas cresçam para sempre
    public class VerificationDataCleaner
    {
        private readonly DatabaseContext _context;
        private readonly TimeProvider _timeProvider;

        public VerificationDataCleaner(DatabaseContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task DeleteExpiredAsync()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var dayStart = now.AddHours(-24);

            await _context.VerificationCodes.Where(c => c.ExpiresAt <= now).ExecuteDeleteAsync();
            await _context.VerificationSendLimits.Where(l => l.DayWindowStartedAt <= dayStart).ExecuteDeleteAsync();
        }
    }
}
