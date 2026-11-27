using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FamilyVaultApi.Data;
using FamilyVaultApi.Middleware;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // API com o seed da demo ligado, ao lado do admin real e dos usuários de teste
    public class DemoApiFixture : ApiFixture
    {
        public const string DemoEmail = "demo@integration.test";
        public const string DemoPassword = "Demo@Senha123";

        public DemoApiFixture() : base(new Dictionary<string, string?>
        {
            ["IpRateLimiting:GeneralRules:2:Limit"] = "1000", // post:/api/app/login
            ["DemoSeed:Enabled"] = "true",
            ["DemoSeed:Email"] = DemoEmail,
            ["DemoSeed:Password"] = DemoPassword
        })
        { }
    }

    public class DemoTests : IClassFixture<DemoApiFixture>
    {
        private const int ExpenseCategoryId = 2;
        private const int ExpenseType = 1;

        private readonly DemoApiFixture _api;

        public DemoTests(DemoApiFixture api) => _api = api;

        [Fact]
        public async Task Seed_ShouldCreateDemoAdministratorAndFamilyWithTransactionsInTheCurrentMonth()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var demoUsers = await context.Users.Where(u => u.IsDemo).ToListAsync();
            demoUsers.Should().HaveCount(1 + DemoSeeder.Members.Count);
            demoUsers.Should().OnlyContain(u => !u.LockoutEnabled);
            demoUsers.Should().ContainSingle(u => u.Email == DemoApiFixture.DemoEmail);
            (await context.Users.AnyAsync(u => u.Email == ApiFixture.AdminEmail && !u.IsDemo)).Should().BeTrue();

            var monthStart = FirstDayOfCurrentMonth();
            var demoIds = demoUsers.Select(u => u.Id).ToList();
            (await context.Transactions.CountAsync(t => demoIds.Contains(t.UserId) && t.TransactionDate >= monthStart))
                .Should().BeGreaterThan(0);
            (await context.Transactions.Where(t => demoIds.Contains(t.UserId)).MinAsync(t => t.TransactionDate))
                .Should().BeOnOrAfter(monthStart.AddMonths(-(DemoSeeder.MonthsOfHistory - 1)));
        }

        [Fact]
        public async Task Seed_WhenRunAgainOnTheSameDay_ShouldNotChangeTheTransactions()
        {
            var before = await DemoTransactionsAsync();

            await RunSeedAsync(DateTime.UtcNow);

            var after = await DemoTransactionsAsync();
            after.Select(t => t.TransactionId).Should().BeEquivalentTo(before.Select(t => t.TransactionId));
        }

        [Fact]
        public async Task Seed_WhenRunOnALaterDay_ShouldRebuildWithoutDuplicating()
        {
            var before = await DemoTransactionsAsync();

            await RunSeedAsync(DateTime.UtcNow.AddDays(1));

            var after = await DemoTransactionsAsync();
            after.Should().NotBeEmpty();
            after.Select(t => t.TransactionId).Should().NotIntersectWith(before.Select(t => t.TransactionId));
            // Refazer apaga as anteriores: nenhum lançamento aparece duas vezes
            after.GroupBy(t => (t.UserId, t.Description, t.TransactionDate)).Should().OnlyContain(g => g.Count() == 1);
        }

        [Fact]
        public async Task Seed_WhenDemoEmailBelongsToARealAccount_ShouldNotTurnItIntoDemo()
        {
            using var factory = new FamilyVaultApiFactory(ApiFixture.AdminEmail, ApiFixture.AdminPassword, settings: new Dictionary<string, string?>
            {
                ["DemoSeed:Enabled"] = "true",
                ["DemoSeed:Email"] = ApiFixture.AdminEmail,
                ["DemoSeed:Password"] = DemoApiFixture.DemoPassword
            });

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            (await context.Users.AnyAsync(u => u.IsDemo)).Should().BeFalse();
            (await context.Transactions.AnyAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task Users_WhenCallerIsDemo_ShouldListOnlyDemoUsers()
        {
            await _api.NewUserAsync();
            var demo = await DemoClientAsync();

            var response = await demo.GetAsync("/api/user?pageSize=100");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = (await response.ReadEnvelopeAsync<PagedData<UserData>>()).Data!;
            page.TotalCount.Should().Be(1 + DemoSeeder.Members.Count);
            page.Items.Should().NotContain(u => u.Email == ApiFixture.AdminEmail);
            page.Items.Select(u => u.UserId).Should().BeSubsetOf(await DemoUserIdsAsync());
        }

        [Fact]
        public async Task Users_WhenCallerIsTheRealAdministrator_ShouldStillListEveryone()
        {
            var admin = await _api.AdminClientAsync();

            var page = (await (await admin.GetAsync("/api/user?pageSize=100")).ReadEnvelopeAsync<PagedData<UserData>>()).Data!;

            page.Items.Should().Contain(u => u.Email == ApiFixture.AdminEmail);
            page.Items.Should().Contain(u => u.Email == DemoApiFixture.DemoEmail);
        }

        [Fact]
        public async Task Transactions_WhenCallerIsDemo_ShouldListOnlyDemoTransactions()
        {
            var (realUserId, _) = await RealTransactionAsync();
            var demo = await DemoClientAsync();

            var all = (await (await demo.GetAsync("/api/transaction?pageSize=100")).ReadEnvelopeAsync<PagedData<TransactionData>>()).Data!;
            var ofRealUser = (await (await demo.GetAsync($"/api/transaction?userId={realUserId}")).ReadEnvelopeAsync<PagedData<TransactionData>>()).Data!;

            all.TotalCount.Should().BeGreaterThan(0);
            all.Items.Select(t => t.UserId).Should().BeSubsetOf(await DemoUserIdsAsync());
            ofRealUser.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task Permissions_WhenCallerIsDemoAndUserIsNotDemo_ShouldAnswer404()
        {
            var (realUserId, _) = await _api.NewUserAsync();
            var demoMemberId = (await DemoUserIdsAsync()).First();
            var demo = await DemoClientAsync();

            (await demo.GetAsync($"/api/user/{realUserId}/permissions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await demo.GetAsync($"/api/user/{demoMemberId}/permissions")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Theory]
        [InlineData("POST", "/api/transaction")]
        [InlineData("PUT", "/api/transaction/1")]
        [InlineData("DELETE", "/api/transaction/1")]
        [InlineData("POST", "/api/category")]
        [InlineData("POST", "/api/user")]
        [InlineData("DELETE", "/api/user/qualquer")]
        [InlineData("POST", "/api/charge/upload")]
        [InlineData("POST", "/api/gmail/connect")]
        [InlineData("POST", "/api/user/me/mcp-tokens")]
        [InlineData("POST", "/api/password/change")]
        public async Task Writes_WhenCallerIsDemo_ShouldAnswer403ReadOnly(string method, string path)
        {
            var demo = await DemoClientAsync();

            var response = await demo.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = JsonContent.Create(new { })
            });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be(DemoReadOnlyMiddleware.Message);
        }

        [Fact]
        public async Task Logout_WhenCallerIsDemo_ShouldStillWork()
        {
            var (token, refreshToken) = await DemoLoginAsync(DemoApiFixture.DemoPassword);
            var demo = ClientWithToken(token);

            var response = await demo.PostAsJsonAsync("/api/logout", new { refreshToken });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Login_WhenDemoPasswordIsWrongManyTimes_ShouldNotLockTheDemoAccount()
        {
            var client = _api.AnonymousClient();
            for (var i = 0; i < 6; i++)
            {
                var wrong = await client.PostAsJsonAsync("/api/app/login", new { email = DemoApiFixture.DemoEmail, password = "Errada@123" });
                wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }

            var (token, _) = await DemoLoginAsync(DemoApiFixture.DemoPassword);

            token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Writes_WhenCallerIsNotDemo_ShouldNotBeBlocked()
        {
            var (_, user) = await _api.UserClientAsync(0);

            var response = await user.PostAsJsonAsync("/api/transaction", NewTransaction("Gasto real"));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        private async Task<HttpClient> DemoClientAsync() =>
            ClientWithToken((await DemoLoginAsync(DemoApiFixture.DemoPassword)).Token);

        private async Task<(string Token, string? RefreshToken)> DemoLoginAsync(string password)
        {
            var response = await _api.AnonymousClient().PostAsJsonAsync("/api/app/login", new { email = DemoApiFixture.DemoEmail, password });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = (await response.ReadEnvelopeAsync<AuthData>()).Data!;
            return (auth.Token, auth.RefreshToken);
        }

        private HttpClient ClientWithToken(string token)
        {
            var client = _api.AnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // Transação grande de um usuário real no mês corrente, que a demo não pode ver nem somar
        private async Task<(string UserId, int TransactionId)> RealTransactionAsync()
        {
            var (userId, user) = await _api.UserClientAsync(1);
            var response = await user.PostAsJsonAsync("/api/transaction", NewTransaction($"Real {Guid.NewGuid():N}", 99999m));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (userId, (await response.ReadEnvelopeAsync<TransactionData>()).Data!.TransactionId);
        }

        private static object NewTransaction(string description, decimal amount = 10m) => new
        {
            categoryId = ExpenseCategoryId,
            transactionTypeId = ExpenseType,
            description,
            amount,
            transactionDate = DateTime.UtcNow.Date
        };

        private async Task RunSeedAsync(DateTime utcNow)
        {
            using var scope = _api.Factory.Services.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            await DemoSeeder.SeedAsync(scope.ServiceProvider, configuration, utcNow, NullLogger.Instance);
        }

        private async Task<List<string>> DemoUserIdsAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            return await context.Users.Where(u => u.IsDemo).Select(u => u.Id).ToListAsync();
        }

        private async Task<List<FamilyVaultApi.Data.Entities.Transaction>> DemoTransactionsAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            return await context.Transactions.AsNoTracking().Where(t => t.User.IsDemo).ToListAsync();
        }

        private static DateTime FirstDayOfCurrentMonth()
        {
            var today = DateTime.UtcNow;
            return new DateTime(today.Year, today.Month, 1);
        }

        private sealed record AuthData(string UserId, string Token, string? RefreshToken);
        private sealed record PagedData<T>(int TotalCount, List<T> Items);
        private sealed record UserData(string UserId, string? Email);
        private sealed record TransactionData(int TransactionId, string UserId);
    }
}
