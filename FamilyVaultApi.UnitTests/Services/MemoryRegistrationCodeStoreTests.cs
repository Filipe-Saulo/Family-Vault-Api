using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FamilyVaultApi.UnitTests.Services
{
    public class MemoryRegistrationCodeStoreTests
    {
        private const string Phone = "+5511987654312";
        private const string Email = "fulano@exemplo.com";

        private readonly FakeTimeProvider _time = new();
        private readonly MemoryRegistrationCodeStore _store;

        public MemoryRegistrationCodeStoreTests()
        {
            _store = new MemoryRegistrationCodeStore(new MemoryCache(new MemoryCacheOptions()), _time);
        }

        private static string WrongCodeFor(string code) => code == "000000" ? "111111" : "000000";

        [Fact]
        public void Issue_ShouldReturnSixDigitCode()
        {
            _store.Issue(Phone).Should().MatchRegex("^[0-9]{6}$");
        }

        [Fact]
        public void Verify_WhenCodeIsCorrect_ShouldReturnTrueAndKeepCodeUntilRemoved()
        {
            var code = _store.Issue(Phone);

            _store.Verify(Phone, code).Should().BeTrue();
            _store.Verify(Phone, code).Should().BeTrue();
        }

        [Fact]
        public void Verify_WhenNothingWasIssued_ShouldReturnFalse()
        {
            _store.Verify(Phone, "123456").Should().BeFalse();
        }

        [Fact]
        public void Verify_WhenCodeIsWrong_ShouldReturnFalse()
        {
            var code = _store.Issue(Phone);

            _store.Verify(Phone, WrongCodeFor(code)).Should().BeFalse();
        }

        [Fact]
        public void Verify_WhenNineMinutesPassed_ShouldStillAccept()
        {
            var code = _store.Issue(Phone);

            _time.Advance(TimeSpan.FromMinutes(9));

            _store.Verify(Phone, code).Should().BeTrue();
        }

        [Fact]
        public void Verify_WhenTenMinutesPassed_ShouldReturnFalse()
        {
            var code = _store.Issue(Phone);

            _time.Advance(TimeSpan.FromMinutes(10));

            _store.Verify(Phone, code).Should().BeFalse();
        }

        [Fact]
        public void Verify_WhenFiveWrongAttemptsWereMade_ShouldInvalidateEvenTheCorrectCode()
        {
            var code = _store.Issue(Phone);
            for (var i = 0; i < 5; i++)
                _store.Verify(Phone, WrongCodeFor(code)).Should().BeFalse();

            _store.Verify(Phone, code).Should().BeFalse();
        }

        [Fact]
        public void Verify_WhenFourWrongAttemptsWereMade_ShouldStillAcceptTheCorrectCode()
        {
            var code = _store.Issue(Phone);
            for (var i = 0; i < 4; i++)
                _store.Verify(Phone, WrongCodeFor(code));

            _store.Verify(Phone, code).Should().BeTrue();
        }

        [Fact]
        public void Remove_ShouldInvalidateTheCode()
        {
            var code = _store.Issue(Phone);

            _store.Remove(Phone);

            _store.Verify(Phone, code).Should().BeFalse();
        }

        [Fact]
        public void Issue_WhenIssuedAgain_ShouldResetFailuresAndExpiry()
        {
            var first = _store.Issue(Phone);
            for (var i = 0; i < 4; i++)
                _store.Verify(Phone, WrongCodeFor(first));
            _time.Advance(TimeSpan.FromMinutes(9));

            var second = _store.Issue(Phone);
            _time.Advance(TimeSpan.FromMinutes(5));

            _store.Verify(Phone, second).Should().BeTrue();
        }

        [Fact]
        public void Verify_WhenDestinationsDiffer_ShouldKeepCodesSeparate()
        {
            var phoneCode = _store.Issue(Phone);
            var emailCode = _store.Issue(Email);

            _store.Verify(Email, emailCode).Should().BeTrue();
            _store.Remove(Email);

            _store.Verify(Email, emailCode).Should().BeFalse();
            _store.Verify(Phone, phoneCode).Should().BeTrue();
        }

        [Fact]
        public void Verify_WhenFailuresHappenForOneDestination_ShouldNotAffectAnother()
        {
            var phoneCode = _store.Issue(Phone);
            var emailCode = _store.Issue(Email);
            for (var i = 0; i < 5; i++)
                _store.Verify(Phone, WrongCodeFor(phoneCode));

            _store.Verify(Email, emailCode).Should().BeTrue();
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan by) => _now += by;
        }
    }
}
