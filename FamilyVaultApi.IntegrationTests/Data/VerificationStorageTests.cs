using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.Repository;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.IntegrationTests.Data
{
    // ExecuteUpdate/ExecuteDelete precisam de banco relacional: rodam em SQLite, com relógio controlado
    public class VerificationStorageTests : IDisposable
    {
        private const string Phone = "+5511987654312";
        private const string OtherPhone = "+5511987654313";
        private const string Foreign = "+14155552671";
        private const string Email = "fulano@exemplo.com";
        private const VerificationPurpose Purpose = VerificationPurpose.Registration;

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DatabaseContext _context;
        private readonly FakeTimeProvider _time = new();

        public VerificationStorageTests()
        {
            _connection.Open();
            _context = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(_connection).Options);
            _context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        private DbRegistrationCodeStore Store(string key = "chave-de-teste-com-mais-de-32-bytes-aqui") =>
            new(_context, _time, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:Key"] = key })
                .Build());

        private DbVerificationSendLimiter Limiter(int globalSmsPerDay = 200) =>
            new(_context, _time, Options.Create(new VerificationSendLimitOptions { MinIntervalSeconds = 60, PerDay = 5, GlobalSmsPerDay = globalSmsPerDay }));

        private async Task SendTimesAsync(DbVerificationSendLimiter limiter, int times)
        {
            for (var i = 0; i < times; i++)
            {
                (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();
                _time.Advance(TimeSpan.FromMinutes(1));
            }
        }

        private static string Wrong(string code) => code == "000000" ? "111111" : "000000";

        // ── Código de cadastro ───────────────────────────────────────────────

        [Fact]
        public async Task VerifyAsync_WhenCodeIsRight_ShouldAccept()
        {
            var store = Store();
            var code = await store.IssueAsync(Email, Purpose);

            (await store.VerifyAsync(Email, Purpose, code)).Should().BeTrue();
        }

        [Fact]
        public async Task IssueAsync_ShouldStoreAKeyedHashAndNeverTheCode()
        {
            var code = await Store().IssueAsync(Email, Purpose);

            var row = await _context.VerificationCodes.AsNoTracking().SingleAsync();

            row.CodeHash.Should().MatchRegex("^[0-9a-f]{64}$").And.NotContain(code);
            row.ExpiresAt.Should().Be(_time.GetUtcNow().UtcDateTime.AddMinutes(10));
        }

        [Fact]
        public async Task VerifyAsync_WhenHashingKeyChanged_ShouldReject()
        {
            var code = await Store().IssueAsync(Email, Purpose);

            (await Store("outra-chave-de-teste-com-mais-de-32-bytes").VerifyAsync(Email, Purpose, code)).Should().BeFalse();
        }

        [Fact]
        public async Task VerifyAsync_WhenCodeExpired_ShouldReject()
        {
            var store = Store();
            var code = await store.IssueAsync(Email, Purpose);

            _time.Advance(TimeSpan.FromMinutes(10));

            (await store.VerifyAsync(Email, Purpose, code)).Should().BeFalse();
        }

        [Fact]
        public async Task VerifyAsync_AfterFiveFailures_ShouldInvalidateEvenTheRightCode()
        {
            var store = Store();
            var code = await store.IssueAsync(Email, Purpose);

            for (var i = 0; i < 5; i++)
                (await store.VerifyAsync(Email, Purpose, Wrong(code))).Should().BeFalse();

            (await store.VerifyAsync(Email, Purpose, code)).Should().BeFalse();
            (await _context.VerificationCodes.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task VerifyAsync_WhenRightCodeIsCheckedRepeatedly_ShouldNotConsumeAttempts()
        {
            var store = Store();
            var code = await store.IssueAsync(Email, Purpose);

            for (var i = 0; i < 7; i++)
                (await store.VerifyAsync(Email, Purpose, code)).Should().BeTrue();

            (await _context.VerificationCodes.AsNoTracking().SingleAsync()).FailedAttempts.Should().Be(0);
        }

        [Fact]
        public async Task IssueAsync_WhenCalledAgain_ShouldReplaceCodeAndResetFailures()
        {
            var store = Store();
            var first = await store.IssueAsync(Email, Purpose);
            await store.VerifyAsync(Email, Purpose, Wrong(first));

            var second = await store.IssueAsync(Email, Purpose);

            var row = await _context.VerificationCodes.AsNoTracking().SingleAsync();
            row.FailedAttempts.Should().Be(0);
            (await store.VerifyAsync(Email, Purpose, second)).Should().BeTrue();
        }

        [Fact]
        public async Task RemoveAsync_ShouldMakeTheCodeInvalid()
        {
            var store = Store();
            var code = await store.IssueAsync(Email, Purpose);

            await store.RemoveAsync(Email, Purpose);

            (await store.VerifyAsync(Email, Purpose, code)).Should().BeFalse();
        }

        // ── Limite de envios ─────────────────────────────────────────────────

        [Fact]
        public async Task TryAcquireAsync_WhenSecondSendIsInsideTheInterval_ShouldDeny()
        {
            var limiter = Limiter();
            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();

            _time.Advance(TimeSpan.FromSeconds(59));

            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeFalse();
        }

        [Fact]
        public async Task TryAcquireAsync_WhenIntervalHasPassed_ShouldAllow()
        {
            var limiter = Limiter();
            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();

            _time.Advance(TimeSpan.FromMinutes(1));

            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();
        }

        [Fact]
        public async Task TryAcquireAsync_WhenDailyLimitIsReached_ShouldDeny()
        {
            var limiter = Limiter();
            await SendTimesAsync(limiter, 5);

            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeFalse();
        }

        [Fact]
        public async Task TryAcquireAsync_WhenDayWindowHasPassed_ShouldAllowAndRestartTheCount()
        {
            var limiter = Limiter();
            await SendTimesAsync(limiter, 5);

            _time.Advance(TimeSpan.FromHours(24));

            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();
            var row = await _context.VerificationSendLimits.AsNoTracking().SingleAsync(l => l.Destination == Phone);
            row.DayCount.Should().Be(1);
            row.DayWindowStartedAt.Should().Be(_time.GetUtcNow().UtcDateTime);
        }

        [Fact]
        public async Task TryAcquireAsync_WhenSmsIsNotForBrazil_ShouldDenyWithoutCounting()
        {
            (await Limiter().TryAcquireAsync(VerificationChannel.Sms, Foreign)).Should().BeFalse();

            (await _context.VerificationSendLimits.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task TryAcquireAsync_WhenGlobalSmsCapIsReached_ShouldDenySmsButNotEmail()
        {
            var limiter = Limiter(globalSmsPerDay: 1);
            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();

            (await limiter.TryAcquireAsync(VerificationChannel.Sms, OtherPhone)).Should().BeFalse();
            (await limiter.TryAcquireAsync(VerificationChannel.Email, Email)).Should().BeTrue();
        }

        [Fact]
        public async Task TryAcquireAsync_WhenGlobalSmsCapIsReached_ShouldNotSpendTheDestinationQuota()
        {
            var limiter = Limiter(globalSmsPerDay: 1);
            await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone);

            await limiter.TryAcquireAsync(VerificationChannel.Sms, OtherPhone);

            (await _context.VerificationSendLimits.AnyAsync(l => l.Destination == OtherPhone)).Should().BeFalse();
        }

        [Fact]
        public async Task TryAcquireAsync_WhenDestinationsAreDifferent_ShouldCountSeparately()
        {
            var limiter = Limiter();

            (await limiter.TryAcquireAsync(VerificationChannel.Sms, Phone)).Should().BeTrue();
            (await limiter.TryAcquireAsync(VerificationChannel.Sms, OtherPhone)).Should().BeTrue();
        }

        // ── Limpeza ──────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteExpiredAsync_ShouldRemoveOnlyWhatExpired()
        {
            var now = _time.GetUtcNow().UtcDateTime;
            _context.VerificationCodes.AddRange(
                new VerificationCode { Destination = "vencido", Purpose = Purpose, CodeHash = "a", ExpiresAt = now.AddSeconds(-1), CreatedAt = now },
                new VerificationCode { Destination = "valido", Purpose = Purpose, CodeHash = "b", ExpiresAt = now.AddMinutes(1), CreatedAt = now });
            _context.VerificationSendLimits.AddRange(
                new VerificationSendLimit { Destination = "vencido", LastSentAt = now.AddHours(-25), DayWindowStartedAt = now.AddHours(-25), DayCount = 5 },
                new VerificationSendLimit { Destination = "valido", LastSentAt = now.AddHours(-1), DayWindowStartedAt = now.AddHours(-2), DayCount = 5 });
            await _context.SaveChangesAsync();

            await new VerificationDataCleaner(_context, _time).DeleteExpiredAsync();

            (await _context.VerificationCodes.AsNoTracking().SingleAsync()).Destination.Should().Be("valido");
            (await _context.VerificationSendLimits.AsNoTracking().SingleAsync()).Destination.Should().Be("valido");
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan by) => _now += by;
        }
    }
}
