using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    public class AuthTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public AuthTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task AppLogin_WithSeededAdministrator_ShouldReturnToken()
        {
            var response = await _api.AnonymousClient().PostAsJsonAsync("/api/app/login",
                new { email = ApiFixture.AdminEmail, password = ApiFixture.AdminPassword });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var envelope = await response.ReadEnvelopeAsync<LoginData>();
            envelope.Data!.Token.Should().NotBeNullOrWhiteSpace();
            envelope.Data.UserId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AppLogin_WithWrongPassword_ShouldReturnGenericUnauthorizedEnvelope()
        {
            var response = await _api.AnonymousClient().PostAsJsonAsync("/api/app/login",
                new { email = ApiFixture.AdminEmail, password = "Errada@123" });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var envelope = await response.ReadEnvelopeAsync<object>();
            envelope.Message.Should().Be("Usuário ou senha inválidos.");
            envelope.TraceId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task ProtectedRoute_WithoutToken_ShouldReturnUnauthorizedEnvelope()
        {
            var response = await _api.AnonymousClient().GetAsync("/api/category");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var envelope = await response.ReadEnvelopeAsync<string>();
            envelope.Message.Should().Be("Acesso negado.");
            envelope.Data.Should().BeNull();
            envelope.TraceId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AdministratorRoute_WithUserToken_ShouldReturnForbiddenEnvelope()
        {
            var (userId, user) = await _api.UserClientAsync(0);

            var response = await user.GetAsync($"/api/user/{userId}/permissions");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var envelope = await response.ReadEnvelopeAsync<string>();
            envelope.Message.Should().Be("Você não tem permissão para acessar este recurso.");
            envelope.Data.Should().BeNull();
            envelope.TraceId.Should().NotBeNullOrWhiteSpace();
        }

        private sealed record LoginData(string UserId, string Token);
    }
}
