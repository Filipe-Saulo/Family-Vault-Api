using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FamilyVaultApi.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyVaultApi.IntegrationTests.Http
{
    internal sealed record SessionData(string UserId, string Token, string? RefreshToken);

    internal static class SessionFlows
    {
        public static async Task<SessionData> AppLoginAsync(ApiFixture api, string phone)
        {
            var response = await api.AnonymousClient().PostAsJsonAsync("/api/app/login",
                new { phone, password = ApiFixture.UserPassword });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<SessionData>()).Data!;
        }

        public static Task<HttpResponseMessage> AppRefreshAsync(ApiFixture api, SessionData session)
        {
            var client = api.AnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            return client.PostAsJsonAsync("/api/app/refreshtoken", new { refreshToken = session.RefreshToken });
        }

        public static Task<HttpResponseMessage> AppLogoutAsync(ApiFixture api, SessionData session) =>
            api.AnonymousClient().PostAsJsonAsync("/api/logout", new { refreshToken = session.RefreshToken });
    }

    // Cada classe tem a própria API porque o login é limitado a 5 por minuto
    public class SessionLogoutTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public SessionLogoutTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task Logout_OnOneDevice_ShouldKeepTheOtherDeviceLoggedIn()
        {
            var (_, phone) = await _api.NewUserAsync();
            var phoneA = await SessionFlows.AppLoginAsync(_api, phone);
            var phoneB = await SessionFlows.AppLoginAsync(_api, phone);

            (await SessionFlows.AppLogoutAsync(_api, phoneA)).StatusCode.Should().Be(HttpStatusCode.OK);

            (await SessionFlows.AppRefreshAsync(_api, phoneA)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await SessionFlows.AppRefreshAsync(_api, phoneB)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task WebLogout_WithCookie_ShouldKeepTheAppLoggedIn_AndWebCookieShouldNotWorkOnAppRoute()
        {
            var (_, phone) = await _api.NewUserAsync();
            var app = await SessionFlows.AppLoginAsync(_api, phone);

            var webLogin = await _api.AnonymousClient().PostAsJsonAsync("/api/web/login",
                new { phone, password = ApiFixture.UserPassword });
            webLogin.StatusCode.Should().Be(HttpStatusCode.OK);
            var cookie = webLogin.Headers.GetValues("Set-Cookie")
                .Single(c => c.StartsWith("refreshToken=") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase)
                    && !c.StartsWith("refreshToken=;"))
                .Split(';')[0];
            var webRefresh = cookie["refreshToken=".Length..];

            // o refresh do web não é aceito pela rota do app
            var crossClient = await SessionFlows.AppRefreshAsync(_api, app with { RefreshToken = webRefresh });
            crossClient.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var logout = new HttpRequestMessage(HttpMethod.Post, "/api/logout")
            {
                Content = JsonContent.Create(new { }),
            };
            logout.Headers.Add("Cookie", cookie);
            (await _api.AnonymousClient().SendAsync(logout)).StatusCode.Should().Be(HttpStatusCode.OK);

            await using var scope = _api.Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var rows = await db.RefreshTokens.AsNoTracking().ToListAsync();
            rows.Should().Contain(t => t.Client == Models.Internal.Enums.AuthClient.Web && t.RevokedAt != null);

            (await SessionFlows.AppRefreshAsync(_api, app)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Logout_WithoutOrWithUnknownToken_ShouldSucceed()
        {
            (await _api.AnonymousClient().PostAsJsonAsync("/api/logout", new { }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await _api.AnonymousClient().PostAsJsonAsync("/api/logout", new { refreshToken = "desconhecido" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class SessionRotationTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public SessionRotationTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task Refresh_ShouldReturnANewToken_AndConcurrentRefreshesShouldBothSucceed()
        {
            var (_, phone) = await _api.NewUserAsync();
            var session = await SessionFlows.AppLoginAsync(_api, phone);

            var responses = await Task.WhenAll(
                SessionFlows.AppRefreshAsync(_api, session),
                SessionFlows.AppRefreshAsync(_api, session));

            responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
            var first = (await responses[0].ReadEnvelopeAsync<SessionData>()).Data!;
            var second = (await responses[1].ReadEnvelopeAsync<SessionData>()).Data!;
            first.RefreshToken.Should().NotBe(session.RefreshToken).And.NotBe(second.RefreshToken);

            (await SessionFlows.AppRefreshAsync(_api, session with { Token = first.Token, RefreshToken = first.RefreshToken }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Refresh_ReusedAfterGraceWindow_ShouldEndOnlyThatSession()
        {
            var (userId, phone) = await _api.NewUserAsync();
            var stolen = await SessionFlows.AppLoginAsync(_api, phone);
            var otherDevice = await SessionFlows.AppLoginAsync(_api, phone);

            var rotated = await SessionFlows.AppRefreshAsync(_api, stolen);
            rotated.StatusCode.Should().Be(HttpStatusCode.OK);
            var current = (await rotated.ReadEnvelopeAsync<SessionData>()).Data!;

            await using (var scope = _api.Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var consumedAt = DateTime.UtcNow.AddMinutes(-1);
                await db.RefreshTokens
                    .Where(t => t.UserId == userId && t.ConsumedAt != null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, consumedAt));
            }

            (await SessionFlows.AppRefreshAsync(_api, stolen)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await SessionFlows.AppRefreshAsync(_api, stolen with { Token = current.Token, RefreshToken = current.RefreshToken }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            (await SessionFlows.AppRefreshAsync(_api, otherDevice)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ChangePassword_ShouldEndEverySession()
        {
            var (_, phone) = await _api.NewUserAsync();
            var session = await SessionFlows.AppLoginAsync(_api, phone);

            var client = _api.AnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            var change = await client.PostAsJsonAsync("/api/password/change", new
            {
                currentPassword = ApiFixture.UserPassword,
                password = "Senha@Nova123",
                passwordConfirm = "Senha@Nova123",
            });
            change.StatusCode.Should().Be(HttpStatusCode.OK);

            (await SessionFlows.AppRefreshAsync(_api, session)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
