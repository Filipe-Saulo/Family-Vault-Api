using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.Services.Service
{
    // Janelas móveis de 1 minuto e 24 horas, em memória: zera ao reiniciar e vale para uma instância só
    public class MemoryVerificationSendLimiter : IVerificationSendLimiter
    {
        private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan Day = TimeSpan.FromHours(24);

        private readonly IMemoryCache _cache;
        private readonly TimeProvider _timeProvider;
        private readonly VerificationSendLimitOptions _options;
        private readonly object _lock = new();

        public MemoryVerificationSendLimiter(IMemoryCache cache, TimeProvider timeProvider, IOptions<VerificationSendLimitOptions> options)
        {
            _cache = cache;
            _timeProvider = timeProvider;
            _options = options.Value;
        }

        public bool TryAcquire(string destination)
        {
            var key = $"verification-send:{destination}";
            var now = _timeProvider.GetUtcNow();

            lock (_lock)
            {
                var sends = _cache.Get<List<DateTimeOffset>>(key) ?? new List<DateTimeOffset>();
                sends.RemoveAll(sentAt => now - sentAt >= Day);

                if (sends.Count >= _options.PerDay || sends.Count(sentAt => now - sentAt < Minute) >= _options.PerMinute)
                    return false;

                sends.Add(now);
                _cache.Set(key, sends, Day);
                return true;
            }
        }
    }
}
