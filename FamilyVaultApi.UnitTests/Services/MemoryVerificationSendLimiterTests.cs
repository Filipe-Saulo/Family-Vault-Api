using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.UnitTests.Services
{
    public class MemoryVerificationSendLimiterTests
    {
        private const string Phone = "+5511987654312";
        private const string OtherPhone = "+5511987654313";

        private readonly FakeTimeProvider _time = new();
        private readonly MemoryVerificationSendLimiter _limiter;

        public MemoryVerificationSendLimiterTests()
        {
            _limiter = new MemoryVerificationSendLimiter(
                new MemoryCache(new MemoryCacheOptions()),
                _time,
                Options.Create(new VerificationSendLimitOptions { PerMinute = 1, PerDay = 5 }));
        }

        private void SendAcquiredTimes(int times)
        {
            for (var i = 0; i < times; i++)
            {
                _limiter.TryAcquire(Phone).Should().BeTrue();
                _time.Advance(TimeSpan.FromMinutes(1));
            }
        }

        [Fact]
        public void TryAcquire_WhenSecondSendIsWithinTheSameMinute_ShouldDeny()
        {
            _limiter.TryAcquire(Phone).Should().BeTrue();

            _time.Advance(TimeSpan.FromSeconds(59));

            _limiter.TryAcquire(Phone).Should().BeFalse();
        }

        [Fact]
        public void TryAcquire_WhenOneMinuteHasPassed_ShouldAllow()
        {
            _limiter.TryAcquire(Phone).Should().BeTrue();

            _time.Advance(TimeSpan.FromMinutes(1));

            _limiter.TryAcquire(Phone).Should().BeTrue();
        }

        [Fact]
        public void TryAcquire_WhenDailyLimitIsReached_ShouldDenyEvenAfterTheMinute()
        {
            SendAcquiredTimes(5);

            _limiter.TryAcquire(Phone).Should().BeFalse();
        }

        [Fact]
        public void TryAcquire_WhenOldestSendLeavesThe24HourWindow_ShouldAllowAgain()
        {
            SendAcquiredTimes(5);

            _time.Advance(TimeSpan.FromHours(24) - TimeSpan.FromMinutes(5));

            _limiter.TryAcquire(Phone).Should().BeTrue();
        }

        [Fact]
        public void TryAcquire_WhenDenied_ShouldNotExtendTheWindow()
        {
            _limiter.TryAcquire(Phone).Should().BeTrue();
            _time.Advance(TimeSpan.FromSeconds(30));
            _limiter.TryAcquire(Phone).Should().BeFalse();

            _time.Advance(TimeSpan.FromSeconds(30));

            _limiter.TryAcquire(Phone).Should().BeTrue();
        }

        [Fact]
        public void TryAcquire_WhenPhonesAreDifferent_ShouldCountSeparately()
        {
            _limiter.TryAcquire(Phone).Should().BeTrue();

            _limiter.TryAcquire(OtherPhone).Should().BeTrue();
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan by) => _now += by;
        }
    }
}
