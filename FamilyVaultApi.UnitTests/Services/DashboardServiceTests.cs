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
    }
}
