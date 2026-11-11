using FamilyVaultApi.Mcp;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Requests.Dashboard;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Dto.Responses.Dashboard;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using ModelContextProtocol;
using Moq;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Services
{
    public class FamilyVaultToolsTests
    {
        private const string UserId = "user-1";

        private readonly Mock<IDashboardService> _dashboardMock = new();
        private readonly Mock<ITransactionService> _transactionMock = new();
        private readonly Mock<ICategoryService> _categoryMock = new();
        private readonly Mock<IChargeService> _chargeMock = new();

        // Principal como o esquema McpToken monta: só uid e escopo, sem papel
        private static ClaimsPrincipal McpPrincipal(string uid = UserId) =>
            new(new ClaimsIdentity([new Claim("uid", uid), new Claim("scope", "mcp:read")], McpTokenAuthenticationHandler.SchemeName));

        private FamilyVaultTools CreateTools(ClaimsPrincipal user) => new(
            _dashboardMock.Object, _transactionMock.Object, _categoryMock.Object, _chargeMock.Object,
            HttpContextAccessorTestHelper.WithUser(user).Object);

        [Fact]
        public async Task GetFinancialSummary_ShouldQueryOwnerAndMapTotals()
        {
            DashboardQueryRequestDto? query = null;
            _dashboardMock.Setup(x => x.GetSummaryAsync(It.IsAny<DashboardQueryRequestDto>(), It.IsAny<ClaimsPrincipal>()))
                .Callback<DashboardQueryRequestDto, ClaimsPrincipal>((q, _) => query = q)
                .ReturnsAsync(new DashboardSummaryDto
                {
                    TotalIncome = 1000m,
                    TotalExpense = 300m,
                    Balance = 700m,
                    ByCategory = [new CategorySummaryItemDto { CategoryId = 2, CategoryDescription = "Mercado", TransactionTypeCode = TransactionTypeCode.Expense, Total = 300m }]
                });

            var result = await CreateTools(McpPrincipal()).GetFinancialSummary(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

            query!.UserId.Should().Be(UserId);
            query.StartDate.Should().Be(new DateOnly(2026, 10, 1));
            query.EndDate.Should().Be(new DateOnly(2026, 10, 31));
            result.Balance.Should().Be(700m);
            result.ByCategory.Should().ContainSingle().Which.Type.Should().Be("expense");
        }

        [Fact]
        public async Task GetFinancialSummary_WithEndBeforeStart_ShouldThrowMcpException()
        {
            var act = () => CreateTools(McpPrincipal()).GetFinancialSummary(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));

            await act.Should().ThrowAsync<McpException>();
            _dashboardMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SearchTransactions_ShouldFixOwnerAndPageSize()
        {
            TransactionQueryRequestDto? query = null;
            _transactionMock.Setup(x => x.GetAllAsync(It.IsAny<TransactionQueryRequestDto>(), It.IsAny<ClaimsPrincipal>()))
                .Callback<TransactionQueryRequestDto, ClaimsPrincipal>((q, _) => query = q)
                .ReturnsAsync(new PagedResult<TransactionResponseDto> { Items = [], PageNumber = 2, PageSize = 20 });

            await CreateTools(McpPrincipal()).SearchTransactions(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 2, "  mercado ", 2);

            query!.UserId.Should().Be(UserId);
            query.PageSize.Should().Be(FamilyVaultTools.TransactionsPageSize);
            query.PageNumber.Should().Be(2);
            query.CategoryId.Should().Be(2);
            query.Description.Should().Be("mercado");
        }

        [Fact]
        public async Task ListPendingCharges_ShouldAskOnlyPendingReviewAndLimitUntrustedFields()
        {
            ChargeQueryRequestDto? query = null;
            _chargeMock.Setup(x => x.GetAllAsync(It.IsAny<ChargeQueryRequestDto>(), It.IsAny<ClaimsPrincipal>()))
                .Callback<ChargeQueryRequestDto, ClaimsPrincipal>((q, _) => query = q)
                .ReturnsAsync(new PagedResult<ChargeResponseDto>
                {
                    Items = [new ChargeResponseDto { ChargeId = 1, Source = "Gmail", Status = "PendingReview", Issuer = "Linha 1\r\nLinha 2" + new string('x', 200), Description = new string('d', 500) }]
                });

            var result = await CreateTools(McpPrincipal()).ListPendingCharges();

            query!.Status.Should().Be(ChargeStatus.PendingReview);
            var charge = result.Should().ContainSingle().Subject;
            charge.Issuer!.Should().StartWith("Linha 1  Linha 2").And.EndWith("…");
            charge.Issuer!.Length.Should().Be(FamilyVaultTools.MaxIssuerLength + 1);
            charge.Description!.Length.Should().Be(FamilyVaultTools.MaxChargeDescriptionLength + 1);
        }

        [Fact]
        public async Task SearchTransactions_ShouldLimitDescriptionAndPage()
        {
            TransactionQueryRequestDto? query = null;
            _transactionMock.Setup(x => x.GetAllAsync(It.IsAny<TransactionQueryRequestDto>(), It.IsAny<ClaimsPrincipal>()))
                .Callback<TransactionQueryRequestDto, ClaimsPrincipal>((q, _) => query = q)
                .ReturnsAsync(new PagedResult<TransactionResponseDto> { Items = [] });

            await CreateTools(McpPrincipal()).SearchTransactions(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, new string('a', 300), 1_000_000);

            query!.Description!.Length.Should().Be(FamilyVaultTools.MaxSearchDescriptionLength);
            query.PageNumber.Should().Be(FamilyVaultTools.MaxPage);
        }

        [Fact]
        public async Task Tool_WithoutUid_ShouldThrowMcpException()
        {
            var act = () => CreateTools(ClaimsPrincipalTestHelper.CreateUnauthenticated()).ListCategories();

            await act.Should().ThrowAsync<McpException>();
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("Enel", "Enel")]
        [InlineData("a\tb", "a b")]
        [InlineData("a\u2028b\u202Ec\u200Bd", "a b c d")]
        public void Untrusted_ShouldKeepShortValuesAndReplaceControlChars(string? input, string? expected)
        {
            FamilyVaultTools.Untrusted(input, 10).Should().Be(expected);
        }
    }
}
