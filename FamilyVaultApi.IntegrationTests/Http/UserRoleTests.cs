using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    internal sealed record RoleUser(string UserId, string? Email, string? PhoneNumber, string? Role);

    internal sealed record RoleUsersPage(List<RoleUser> Items);

    internal static class UserRoleFlows
    {
        private static int _sequence;

        public static string NewEmail() => $"role{Interlocked.Increment(ref _sequence)}@integration.test";

        public static string NewPhone() => $"+55119{76540000 + Interlocked.Increment(ref _sequence)}";

        public static Task<HttpResponseMessage> ChangeRoleAsync(HttpClient client, string userId, object body) =>
            client.PutAsJsonAsync($"/api/user/{userId}/role", body);

        public static async Task<RoleUser> NewAdminAsync(ApiFixture api, string email)
        {
            var admin = await api.AdminClientAsync();
            var created = await admin.PostAsJsonAsync("/api/user", new
            {
                firstName = "Admin",
                lastName = "Teste",
                age = 30,
                password = ApiFixture.UserPassword,
                passwordConfirm = ApiFixture.UserPassword,
                role = "Administrator",
                email
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await created.ReadEnvelopeAsync<RoleUser>()).Data!;
        }

        public static async Task<SessionData> LoginByEmailAsync(ApiFixture api, string email)
        {
            var response = await api.AnonymousClient().PostAsJsonAsync("/api/app/login",
                new { email, password = ApiFixture.UserPassword });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<SessionData>()).Data!;
        }

        public static HttpClient ClientWith(ApiFixture api, string token)
        {
            var client = api.AnonymousClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }

    // Cada classe tem a própria API porque o login é limitado a 5 por minuto
    public class UserRolePromotionTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public UserRolePromotionTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task ChangeRole_PromotingUser_ShouldMoveLoginToEmailAndEndSessions()
        {
            var (userId, phone) = await _api.NewUserAsync();
            var session = await SessionFlows.AppLoginAsync(_api, phone);
            var email = UserRoleFlows.NewEmail();
            var admin = await _api.AdminClientAsync();

            var response = await UserRoleFlows.ChangeRoleAsync(admin, userId, new { role = "Administrator", email });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var changed = (await response.ReadEnvelopeAsync<RoleUser>()).Data!;
            changed.Role.Should().Be("Administrator");
            changed.Email.Should().Be(email);
            changed.PhoneNumber.Should().Be(phone);

            (await SessionFlows.AppRefreshAsync(_api, session)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var promoted = await UserRoleFlows.LoginByEmailAsync(_api, email);
            var users = await UserRoleFlows.ClientWith(_api, promoted.Token).GetAsync("/api/user");
            users.StatusCode.Should().Be(HttpStatusCode.OK);

            var phoneLogin = await _api.AnonymousClient().PostAsJsonAsync("/api/app/login",
                new { phone, password = ApiFixture.UserPassword });
            phoneLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    public class UserRoleDemotionTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public UserRoleDemotionTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task ChangeRole_DemotingAdministrator_ShouldRequirePhoneMoveLoginAndEndSessions()
        {
            var email = UserRoleFlows.NewEmail();
            var target = await UserRoleFlows.NewAdminAsync(_api, email);
            var session = await UserRoleFlows.LoginByEmailAsync(_api, email);
            var admin = await _api.AdminClientAsync();

            var withoutPhone = await UserRoleFlows.ChangeRoleAsync(admin, target.UserId, new { role = "User" });
            withoutPhone.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await withoutPhone.ReadEnvelopeAsync<object>()).Message.Should().Be("O telefone é obrigatório para User.");

            var phone = UserRoleFlows.NewPhone();
            var response = await UserRoleFlows.ChangeRoleAsync(admin, target.UserId, new { role = "User", phoneNumber = phone });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var changed = (await response.ReadEnvelopeAsync<RoleUser>()).Data!;
            changed.Role.Should().Be("User");
            changed.PhoneNumber.Should().Be(phone);
            changed.Email.Should().Be(email);

            (await SessionFlows.AppRefreshAsync(_api, session)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var demoted = await SessionFlows.AppLoginAsync(_api, phone);
            var users = await UserRoleFlows.ClientWith(_api, demoted.Token).GetAsync("/api/user");
            users.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    public class UserRoleRuleTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public UserRoleRuleTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task ChangeRole_WithoutAdministratorRole_ShouldReturnForbidden()
        {
            var (targetId, _) = await _api.NewUserAsync();
            var (_, user) = await _api.UserClientAsync(0);

            var response = await UserRoleFlows.ChangeRoleAsync(user, targetId,
                new { role = "Administrator", email = UserRoleFlows.NewEmail() });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ChangeRole_WithInvalidRole_ShouldReturnBadRequest()
        {
            var (targetId, _) = await _api.NewUserAsync();
            var admin = await _api.AdminClientAsync();

            var response = await UserRoleFlows.ChangeRoleAsync(admin, targetId, new { role = "Root" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ChangeRole_WhenTargetDoesNotExist_ShouldReturnNotFound()
        {
            var admin = await _api.AdminClientAsync();

            var response = await UserRoleFlows.ChangeRoleAsync(admin, "nao-existe", new { role = "User" });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ChangeRole_OnOwnAccount_ShouldReturnBadRequest()
        {
            var admin = await _api.AdminClientAsync();
            var adminId = await FindAdminIdAsync(admin);

            var response = await UserRoleFlows.ChangeRoleAsync(admin, adminId,
                new { role = "User", phoneNumber = UserRoleFlows.NewPhone() });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be("Você não pode alterar o próprio papel.");
        }

        [Fact]
        public async Task ChangeRole_WhenAlreadyInRole_ShouldKeepTheSession()
        {
            var (userId, phone) = await _api.NewUserAsync();
            var session = await SessionFlows.AppLoginAsync(_api, phone);
            var admin = await _api.AdminClientAsync();

            var response = await UserRoleFlows.ChangeRoleAsync(admin, userId, new { role = "User" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await SessionFlows.AppRefreshAsync(_api, session)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetUsers_ShouldReturnTheRoleOfEachUser()
        {
            var (userId, _) = await _api.NewUserAsync();
            var admin = await _api.AdminClientAsync();

            var response = await admin.GetAsync("/api/user?pageSize=100");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var users = (await response.ReadEnvelopeAsync<RoleUsersPage>()).Data!.Items;
            users.Single(u => u.UserId == userId).Role.Should().Be("User");
            users.Single(u => u.Email == ApiFixture.AdminEmail).Role.Should().Be("Administrator");
        }

        private static async Task<string> FindAdminIdAsync(HttpClient admin)
        {
            var response = await admin.GetAsync("/api/user/me");
            return (await response.ReadEnvelopeAsync<RoleUser>()).Data!.UserId;
        }
    }

    public class UserRoleLastAdministratorTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _api;

        public UserRoleLastAdministratorTests(ApiFixture api) => _api = api;

        // O caminho real até o último admin: o ex-admin ainda tem o access token antigo por até 10 min
        [Fact]
        public async Task ChangeRole_WithStaleToken_ShouldNotDemoteTheLastAdministrator()
        {
            var seeded = await _api.AdminClientAsync();
            var seededId = (await (await seeded.GetAsync("/api/user/me")).ReadEnvelopeAsync<RoleUser>()).Data!.UserId;

            var email = UserRoleFlows.NewEmail();
            var other = await UserRoleFlows.NewAdminAsync(_api, email);
            var otherSession = await UserRoleFlows.LoginByEmailAsync(_api, email);
            var otherClient = UserRoleFlows.ClientWith(_api, otherSession.Token);

            var demoted = await UserRoleFlows.ChangeRoleAsync(otherClient, seededId,
                new { role = "User", phoneNumber = UserRoleFlows.NewPhone() });
            demoted.StatusCode.Should().Be(HttpStatusCode.OK);

            var response = await UserRoleFlows.ChangeRoleAsync(seeded, other.UserId,
                new { role = "User", phoneNumber = UserRoleFlows.NewPhone() });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
    }
}
