using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class DashboardServiceTests
    {
        private readonly Mock<IDashboardRepository> _repositoryMock;
        private readonly DashboardService _service;

        public DashboardServiceTests()
        {
            _repositoryMock = new Mock<IDashboardRepository>();
            _service = new DashboardService(_repositoryMock.Object);
        }

        // ── GetSummaryAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetSummaryAsync_WhenQueryIsNull_ShouldUseDefaultQuery()
        {
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .ReturnsAsync(expected);

            var result = await _service.GetSummaryAsync(null!, ClaimsPrincipalTestHelper.CreateAdmin());

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()), Times.Once);
        }

        [Fact]
        public async Task GetSummaryAsync_AsAdmin_ShouldNotOverrideProvidedUserId()
        {
            var query = new DashboardQueryRequestDto { UserId = "target-user" };
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.Is<DashboardQueryRequestDto>(q => q.UserId == "target-user")))
                .ReturnsAsync(expected);

            var result = await _service.GetSummaryAsync(query, ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            result.Should().Be(expected);
            query.UserId.Should().Be("target-user");
        }

        [Fact]
        public async Task GetSummaryAsync_AsUser_ShouldOverrideUserIdWithCallerId()
        {
            var query = new DashboardQueryRequestDto { UserId = "someone-else" };
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.Is<DashboardQueryRequestDto>(q => q.UserId == "user-1")))
                .ReturnsAsync(expected);

            var result = await _service.GetSummaryAsync(query, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            result.Should().Be(expected);
            query.UserId.Should().Be("user-1");
        }

        // ── GetSummaryAsync — default date range ────────────────────────────

        [Fact]
        public async Task GetSummaryAsync_WhenNoDateRangeProvided_ShouldDefaultToCurrentMonth()
        {
            var now = DateTime.UtcNow;
            var expectedStart = new DateOnly(now.Year, now.Month, 1);
            var expectedEnd = expectedStart.AddMonths(1).AddDays(-1);
            var query = new DashboardQueryRequestDto();
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .ReturnsAsync(expected);

            await _service.GetSummaryAsync(query, ClaimsPrincipalTestHelper.CreateAdmin());

            query.StartDate.Should().Be(expectedStart);
            query.EndDate.Should().Be(expectedEnd);
        }

        [Fact]
        public async Task GetSummaryAsync_WhenOnlyStartDateProvided_ShouldLeaveEndDateOpen()
        {
            var startDate = new DateOnly(2026, 3, 10);
            var query = new DashboardQueryRequestDto { StartDate = startDate };
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .ReturnsAsync(expected);

            await _service.GetSummaryAsync(query, ClaimsPrincipalTestHelper.CreateAdmin());

            query.StartDate.Should().Be(startDate);
            query.EndDate.Should().BeNull();
        }

        [Fact]
        public async Task GetSummaryAsync_WhenOnlyEndDateProvided_ShouldLeaveStartDateOpen()
        {
            var endDate = new DateOnly(2026, 3, 10);
            var query = new DashboardQueryRequestDto { EndDate = endDate };
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .ReturnsAsync(expected);

            await _service.GetSummaryAsync(query, ClaimsPrincipalTestHelper.CreateAdmin());

            query.StartDate.Should().BeNull();
            query.EndDate.Should().Be(endDate);
        }

        [Fact]
        public async Task GetSummaryAsync_WhenBothDatesProvided_ShouldNotOverride()
        {
            var startDate = new DateOnly(2026, 1, 1);
            var endDate = new DateOnly(2026, 1, 31);
            var query = new DashboardQueryRequestDto { StartDate = startDate, EndDate = endDate };
            var expected = new DashboardSummaryDto();

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .ReturnsAsync(expected);

            await _service.GetSummaryAsync(query, ClaimsPrincipalTestHelper.CreateAdmin());

            query.StartDate.Should().Be(startDate);
            query.EndDate.Should().Be(endDate);
        }

        [Fact]
        public async Task GetSummaryAsync_WhenNoDateRangeProvided_ShouldSendFirstAndLastDayOfCurrentMonthToRepository()
        {
            var now = DateTime.UtcNow;
            var expectedStart = new DateOnly(now.Year, now.Month, 1);
            var expectedEnd = new DateOnly(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
            DashboardQueryRequestDto? received = null;

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .Callback<DashboardQueryRequestDto>(q => received = q)
                .ReturnsAsync(new DashboardSummaryDto());

            await _service.GetSummaryAsync(new DashboardQueryRequestDto(), ClaimsPrincipalTestHelper.CreateAdmin());

            received.Should().NotBeNull();
            received!.StartDate.Should().Be(expectedStart);
            received.EndDate.Should().Be(expectedEnd);
        }

        [Fact]
        public async Task GetSummaryAsync_WhenOnlyOneDateProvided_ShouldSendOtherDateAsNullToRepository()
        {
            DashboardQueryRequestDto? received = null;

            _repositoryMock
                .Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>()))
                .Callback<DashboardQueryRequestDto>(q => received = q)
                .ReturnsAsync(new DashboardSummaryDto());

            await _service.GetSummaryAsync(
                new DashboardQueryRequestDto { StartDate = new DateOnly(2026, 3, 10) },
                ClaimsPrincipalTestHelper.CreateAdmin());

            received!.StartDate.Should().Be(new DateOnly(2026, 3, 10));
            received.EndDate.Should().BeNull();
        }
    }
}
