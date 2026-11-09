using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    // Códigos de cadastro em memória: zeram ao reiniciar e valem para uma instância só.
    // Guarda só o hash SHA-256 do código.
    public class MemoryRegistrationCodeStore : IRegistrationCodeStore
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private const int MaxFailures = 5;

        private readonly IMemoryCache _cache;
        private readonly TimeProvider _timeProvider;
        private readonly object _lock = new();

        public MemoryRegistrationCodeStore(IMemoryCache cache, TimeProvider timeProvider)
        {
            _cache = cache;
            _timeProvider = timeProvider;
        }

        public string Issue(string destination)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var entry = new Entry(Hash(code), _timeProvider.GetUtcNow().Add(Lifetime));

            lock (_lock)
            {
                _cache.Set(Key(destination), entry, Lifetime);
            }

            return code;
        }

        public bool Verify(string destination, string code)
        {
            var key = Key(destination);

            lock (_lock)
            {
                if (!_cache.TryGetValue<Entry>(key, out var entry) || entry == null)
                    return false;

                if (_timeProvider.GetUtcNow() >= entry.ExpiresAt)
                {
                    _cache.Remove(key);
                    return false;
                }

                if (CryptographicOperations.FixedTimeEquals(entry.Hash, Hash(code ?? string.Empty)))
                    return true;

                entry.Failures++;
                if (entry.Failures >= MaxFailures)
                    _cache.Remove(key);

                return false;
            }
        }

        public void Remove(string destination)
        {
            lock (_lock)
            {
                _cache.Remove(Key(destination));
            }
        }

        private static string Key(string destination) => $"registration-code:{destination}";

        private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));

        private sealed class Entry
        {
            public Entry(byte[] hash, DateTimeOffset expiresAt)
            {
                Hash = hash;
                ExpiresAt = expiresAt;
            }

            public byte[] Hash { get; }
            public DateTimeOffset ExpiresAt { get; }
            public int Failures { get; set; }
        }
    }
}
