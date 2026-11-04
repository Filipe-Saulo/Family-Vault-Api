using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    public class TransactionTests : IClassFixture<ApiFixture>
    {
        // Seeds do DatabaseContext: categoria 2 é de despesa, categoria 1 é de receita
        private const int ExpenseCategoryId = 2;
        private const int IncomeCategoryId = 1;
        private const int ExpenseType = 1;

        private readonly ApiFixture _api;

        public TransactionTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task Transaction_ByOwner_ShouldBeCreatedListedUpdatedAndDeleted()
        {
            var (userId, user) = await _api.UserClientAsync(0);

            var created = await CreateAsync(user, "Mercado");
            created.UserId.Should().Be(userId);
            (await ListAsync(user)).Should().Contain(t => t.TransactionId == created.TransactionId);

            var put = await user.PutAsJsonAsync($"/api/transaction/{created.TransactionId}", new
            {
                categoryId = ExpenseCategoryId,
                transactionTypeId = ExpenseType,
                description = "Mercado do mês",
                amount = 250.75m,
                transactionDate = new DateTime(2026, 11, 2)
            });
            put.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = (await put.ReadEnvelopeAsync<TransactionData>()).Data!;
            updated.Description.Should().Be("Mercado do mês");
            updated.Amount.Should().Be(250.75m);

            var delete = await user.DeleteAsync($"/api/transaction/{created.TransactionId}");
            delete.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ListAsync(user)).Should().NotContain(t => t.TransactionId == created.TransactionId);
        }

        [Fact]
        public async Task Transaction_OfAnotherUser_ShouldBeHiddenAndForbidden()
        {
            var (ownerId, owner) = await _api.UserClientAsync(0);
            var (_, other) = await _api.UserClientAsync(1);
            var created = await CreateAsync(owner, "Do dono");

            (await ListAsync(other)).Should().NotContain(t => t.TransactionId == created.TransactionId);
            (await ListAsync(other, $"?UserId={ownerId}")).Should().NotContain(t => t.TransactionId == created.TransactionId);

            var put = await other.PutAsJsonAsync($"/api/transaction/{created.TransactionId}", new
            {
                categoryId = ExpenseCategoryId,
                transactionTypeId = ExpenseType,
                description = "Invasor",
                amount = 1m,
                transactionDate = new DateTime(2026, 11, 1)
            });
            put.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var delete = await other.DeleteAsync($"/api/transaction/{created.TransactionId}");
            delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetAll_ByAdministratorFilteringUser_ShouldSeeThatUsersTransaction()
        {
            var (ownerId, owner) = await _api.UserClientAsync(0);
            var admin = await _api.AdminClientAsync();
            var created = await CreateAsync(owner, "Visível ao admin");

            var items = await ListAsync(admin, $"?UserId={ownerId}");

            items.Should().Contain(t => t.TransactionId == created.TransactionId);
            items.Should().OnlyContain(t => t.UserId == ownerId);
        }

        [Fact]
        public async Task Post_WithTypeNotMatchingCategoryPurpose_ShouldReturnBadRequest()
        {
            var (_, user) = await _api.UserClientAsync(0);

            var response = await user.PostAsJsonAsync("/api/transaction", new
            {
                categoryId = IncomeCategoryId,
                transactionTypeId = ExpenseType,
                description = "Tipo errado",
                amount = 10m,
                transactionDate = new DateTime(2026, 11, 1)
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ReadEnvelopeAsync<object>()).TraceId.Should().NotBeNullOrWhiteSpace();
        }

        private static async Task<TransactionData> CreateAsync(HttpClient client, string description)
        {
            var response = await client.PostAsJsonAsync("/api/transaction", new
            {
                categoryId = ExpenseCategoryId,
                transactionTypeId = ExpenseType,
                description,
                amount = 120.50m,
                transactionDate = new DateTime(2026, 11, 1)
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.ReadEnvelopeAsync<TransactionData>()).Data!;
        }

        private static async Task<List<TransactionData>> ListAsync(HttpClient client, string queryString = "")
        {
            // PageSize máximo: um NotContain só vale se a lista vier inteira
            var separator = queryString.Length == 0 ? "?" : "&";
            var response = await client.GetAsync($"/api/transaction{queryString}{separator}PageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = (await response.ReadEnvelopeAsync<PagedData<TransactionData>>()).Data!;
            page.TotalCount.Should().Be(page.Items.Count);
            return page.Items;
        }

        private sealed record PagedData<T>(int TotalCount, List<T> Items);

        private sealed record TransactionData(int TransactionId, string Description, decimal Amount, string UserId);
    }
}
