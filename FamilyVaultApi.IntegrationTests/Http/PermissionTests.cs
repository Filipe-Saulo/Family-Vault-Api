using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    public class PermissionTests : IClassFixture<ApiFixture>
    {
        private const int ManageUsers = 4;

        private readonly ApiFixture _api;

        public PermissionTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task GrantPermission_WithNumberOutsideEnum_ShouldReturnBadRequestAndNotGrant()
        {
            var (userId, _) = await _api.NewUserAsync();
            var admin = await _api.AdminClientAsync();

            var response = await admin.PostAsJsonAsync($"/api/user/{userId}/permissions", new { permission = 99 });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var envelope = await response.ReadEnvelopeAsync<object>();
            envelope.Message.Should().Be("Permissão inválida.");
            envelope.TraceId.Should().NotBeNullOrWhiteSpace();
            (await GetPermissionsAsync(admin, userId)).Should().BeEmpty();
        }

        [Fact]
        public async Task GrantPermission_WithValidNumber_ShouldGrant()
        {
            var (userId, _) = await _api.NewUserAsync();
            var admin = await _api.AdminClientAsync();

            var response = await admin.PostAsJsonAsync($"/api/user/{userId}/permissions", new { permission = ManageUsers });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be("Permissão concedida");
            (await GetPermissionsAsync(admin, userId)).Should().Equal(ManageUsers);
        }

        [Fact]
        public async Task GrantPermission_WithName_ShouldGrant()
        {
            var (userId, _) = await _api.NewUserAsync();
            var admin = await _api.AdminClientAsync();

            var response = await admin.PostAsJsonAsync($"/api/user/{userId}/permissions", new { permission = "manageusers" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await GetPermissionsAsync(admin, userId)).Should().Equal(ManageUsers);
        }

        private static async Task<List<int>> GetPermissionsAsync(HttpClient admin, string userId)
        {
            var response = await admin.GetAsync($"/api/user/{userId}/permissions");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<List<int>>()).Data!;
        }
    }
}
