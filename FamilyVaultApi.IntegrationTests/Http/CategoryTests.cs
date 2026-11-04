using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    public class CategoryTests : IClassFixture<ApiFixture>
    {
        // Seed do DatabaseContext: finalidade 1 é despesa
        private const int ExpensePurposeId = 1;

        private readonly ApiFixture _api;

        public CategoryTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task Post_ByAdministrator_ShouldCreateGlobalCategoryVisibleToUsers()
        {
            var admin = await _api.AdminClientAsync();
            var (_, user) = await _api.UserClientAsync(0);
            var description = Unique("Global");

            var response = await admin.PostAsJsonAsync("/api/category", new { description, categoryPurposeId = ExpensePurposeId });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = (await response.ReadEnvelopeAsync<CategoryData>()).Data!;
            created.IsGlobal.Should().BeTrue();
            created.Purpose!.CategoryPurposeId.Should().Be(ExpensePurposeId);
            (await LookupAsync(user)).Should().Contain(c => c.CategoryId == created.CategoryId);
        }

        [Fact]
        public async Task PersonalCategory_ByOwner_ShouldBeCreatedUpdatedAndDeleted()
        {
            var (_, user) = await _api.UserClientAsync(0);

            var created = await CreateAsync(user, Unique("Pessoal"));
            created.IsGlobal.Should().BeFalse();

            var newDescription = Unique("Editada");
            var put = await user.PutAsJsonAsync($"/api/category/{created.CategoryId}",
                new { description = newDescription, categoryPurposeId = ExpensePurposeId });
            put.StatusCode.Should().Be(HttpStatusCode.OK);
            (await put.ReadEnvelopeAsync<CategoryData>()).Data!.Description.Should().Be(newDescription);

            var delete = await user.DeleteAsync($"/api/category/{created.CategoryId}");
            delete.StatusCode.Should().Be(HttpStatusCode.OK);
            (await LookupAsync(user)).Should().NotContain(c => c.CategoryId == created.CategoryId);
        }

        [Fact]
        public async Task PersonalCategory_OfAnotherUser_ShouldBeHiddenAndNotFoundOnUpdate()
        {
            var (_, owner) = await _api.UserClientAsync(0);
            var (_, other) = await _api.UserClientAsync(1);
            var created = await CreateAsync(owner, Unique("DoDono"));

            (await LookupAsync(other)).Should().NotContain(c => c.CategoryId == created.CategoryId);

            var put = await other.PutAsJsonAsync($"/api/category/{created.CategoryId}",
                new { description = Unique("Invasor"), categoryPurposeId = ExpensePurposeId });
            put.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await put.ReadEnvelopeAsync<object>()).TraceId.Should().NotBeNullOrWhiteSpace();
            (await LookupAsync(owner)).Should().Contain(c => c.CategoryId == created.CategoryId && c.Description == created.Description);
        }

        [Fact]
        public async Task GlobalCategory_UpdatedByUser_ShouldReturnForbidden()
        {
            var admin = await _api.AdminClientAsync();
            var (_, user) = await _api.UserClientAsync(0);
            var global = await CreateAsync(admin, Unique("Global"));

            var put = await user.PutAsJsonAsync($"/api/category/{global.CategoryId}",
                new { description = Unique("Tentativa"), categoryPurposeId = ExpensePurposeId });

            put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await put.ReadEnvelopeAsync<object>()).TraceId.Should().NotBeNullOrWhiteSpace();
            (await LookupAsync(admin)).Should().Contain(c => c.CategoryId == global.CategoryId && c.Description == global.Description);
        }

        private static async Task<CategoryData> CreateAsync(HttpClient client, string description)
        {
            var response = await client.PostAsJsonAsync("/api/category", new { description, categoryPurposeId = ExpensePurposeId });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.ReadEnvelopeAsync<CategoryData>()).Data!;
        }

        private static async Task<List<CategoryData>> LookupAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/category/lookup");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<List<CategoryData>>()).Data!;
        }

        private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

        private sealed record PurposeData(int CategoryPurposeId, string Name);

        private sealed record CategoryData(int CategoryId, string Description, PurposeData? Purpose, bool IsGlobal);
    }
}
