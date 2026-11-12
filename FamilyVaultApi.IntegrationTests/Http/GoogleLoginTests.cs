using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // Login com Google pelo app (POST /api/app/google). O ID token é simulado: o validador falso devolve a
    // identidade cadastrada para cada token, no lugar da chamada ao Google.
    public class GoogleLoginTests
    {
        private const string AdminEmail = "admin@integration.test";
        private const string Password = "Senha@User123";
        private const string NewPassword = "Outra@Senha456";

        private sealed record AuthData(string UserId, string Token, string? RefreshToken);
        private sealed record CreatedUser(string UserId);

        [Fact]
        public async Task Google_WhenNoAccount_ShouldCreateUserWithoutPasswordAndReuseItNextTime()
        {
            using var factory = new GoogleApiFactory();
            factory.Google.Add("token-1", Identity("sub-1", "Nova.Pessoa@Gmail.com"));
            var client = factory.CreateClient();

            var first = await client.PostAsJsonAsync("/api/app/google", new { idToken = "token-1" });
            var second = await client.PostAsJsonAsync("/api/app/google", new { idToken = "token-1" });

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = (await first.ReadEnvelopeAsync<AuthData>()).Data!;
            auth.Token.Should().NotBeNullOrEmpty();
            auth.RefreshToken.Should().NotBeNullOrEmpty();
            (await second.ReadEnvelopeAsync<AuthData>()).Data!.UserId.Should().Be(auth.UserId);

            var (user, roles, hasPassword) = await factory.InspectAsync(auth.UserId);
            user.UserName.Should().Be("nova.pessoa@gmail.com");
            user.Email.Should().Be("nova.pessoa@gmail.com");
            user.EmailConfirmed.Should().BeTrue();
            user.FirstName.Should().Be("Nova");
            hasPassword.Should().BeFalse();
            roles.Should().BeEquivalentTo("User");
        }

        [Fact]
        public async Task Google_WhenEmailHasPhoneAccount_ShouldLinkItAndKeepPhoneLogin()
        {
            using var factory = new GoogleApiFactory(AdminEmail);
            var admin = await LoggedClientAsync(factory, new { email = AdminEmail, password = Password });
            var created = await admin.PostAsJsonAsync("/api/user", new
            {
                firstName = "Com",
                lastName = "Telefone",
                age = 30,
                password = Password,
                passwordConfirm = Password,
                role = "User",
                phoneNumber = "+5511987650001",
                email = "telefone@gmail.com"
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var userId = (await created.ReadEnvelopeAsync<CreatedUser>()).Data!.UserId;
            factory.Google.Add("token-2", Identity("sub-2", "telefone@gmail.com"));
            var client = factory.CreateClient();

            var google = await client.PostAsJsonAsync("/api/app/google", new { idToken = "token-2" });
            var phoneLogin = await client.PostAsJsonAsync("/api/app/login", new { phone = "+5511987650001", password = Password });

            google.StatusCode.Should().Be(HttpStatusCode.OK);
            (await google.ReadEnvelopeAsync<AuthData>()).Data!.UserId.Should().Be(userId);
            phoneLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Google_WhenEmailIsInTwoAccounts_ShouldAnswer409WithoutLinking()
        {
            using var factory = new GoogleApiFactory(AdminEmail);
            var admin = await LoggedClientAsync(factory, new { email = AdminEmail, password = Password });
            foreach (var phone in new[] { "+5511987650002", "+5511987650003" })
            {
                var created = await admin.PostAsJsonAsync("/api/user", new
                {
                    firstName = "Mesmo",
                    lastName = "Email",
                    age = 30,
                    password = Password,
                    passwordConfirm = Password,
                    role = "User",
                    phoneNumber = phone
                });
                created.StatusCode.Should().Be(HttpStatusCode.Created);
                await factory.SetEmailAsync((await created.ReadEnvelopeAsync<CreatedUser>()).Data!.UserId, "repetido@gmail.com");
            }
            factory.Google.Add("token-dup", Identity("sub-dup", "repetido@gmail.com"));

            var response = await factory.CreateClient().PostAsJsonAsync("/api/app/google", new { idToken = "token-dup" });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be("Este e-mail está em mais de uma conta. Entre com telefone ou e-mail e senha.");
            (await factory.HasGoogleLoginAsync("sub-dup")).Should().BeFalse();
        }

        [Fact]
        public async Task Google_WhenEmailBelongsToAdministrator_ShouldAnswer403WithoutLinking()
        {
            using var factory = new GoogleApiFactory(AdminEmail);
            factory.Google.Add("token-admin", Identity("sub-admin", AdminEmail));
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/app/google", new { idToken = "token-admin" });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be("Administrador entra com e-mail e senha.");
            (await factory.HasGoogleLoginAsync("sub-admin")).Should().BeFalse();
        }

        [Fact]
        public async Task Google_WhenTokenIsInvalidOrEmailNotVerified_ShouldAnswer401()
        {
            using var factory = new GoogleApiFactory();
            factory.Google.Add("nao-verificado", Identity("sub-3", "nao.verificado@gmail.com", verified: false));
            var client = factory.CreateClient();

            var unknown = await client.PostAsJsonAsync("/api/app/google", new { idToken = "desconhecido" });
            var unverified = await client.PostAsJsonAsync("/api/app/google", new { idToken = "nao-verificado" });
            var empty = await client.PostAsJsonAsync("/api/app/google", new { idToken = "" });

            unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await unknown.ReadEnvelopeAsync<object>()).Message.Should().Be("Login com Google inválido.");
            unverified.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Google_WhenClientIdsAreNotConfigured_ShouldAnswer503()
        {
            using var factory = new GoogleApiFactory(configured: false);
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/app/google", new { idToken = "qualquer" });

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be("Login com Google não configurado.");
        }

        [Fact]
        public async Task GoogleAccount_ShouldSetFirstPasswordOnlyThroughForgotPassword()
        {
            const string email = "sem.senha@gmail.com";
            using var factory = new GoogleApiFactory();
            factory.Google.Add("token-4", Identity("sub-4", email));
            var client = factory.CreateClient();
            var auth = (await (await client.PostAsJsonAsync("/api/app/google", new { idToken = "token-4" })).ReadEnvelopeAsync<AuthData>()).Data!;

            // Sem senha: o login por senha dá o 401 genérico e a troca logada aponta para o "esqueci a senha"
            var passwordLogin = await client.PostAsJsonAsync("/api/app/login", new { email, password = Password });
            var logged = factory.CreateClient();
            logged.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            var change = await logged.PostAsJsonAsync("/api/password/change", new { currentPassword = "Qualquer@123", password = NewPassword, passwordConfirm = NewPassword });

            passwordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            change.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await change.ReadEnvelopeAsync<object>()).Message.Should().Be("Esta conta ainda não tem senha. Use \"Esqueci a senha\" para criar uma.");

            var forgot = await client.PostAsJsonAsync("/api/password/forgot", new { email });
            forgot.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var code = (await factory.Deliveries.WaitForAsync(email)).Code;
            var reset = await client.PostAsJsonAsync("/api/password/reset", new { email, code, password = NewPassword, passwordConfirm = NewPassword });
            var loginAfter = await client.PostAsJsonAsync("/api/app/login", new { email, password = NewPassword });
            var googleAfter = await client.PostAsJsonAsync("/api/app/google", new { idToken = "token-4" });

            reset.StatusCode.Should().Be(HttpStatusCode.OK);
            loginAfter.StatusCode.Should().Be(HttpStatusCode.OK);
            googleAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        private static GoogleIdentity Identity(string sub, string email, bool verified = true) =>
            new(sub, email, verified, "Nova", "Pessoa", "Nova Pessoa");

        private static async Task<HttpClient> LoggedClientAsync(FamilyVaultApiFactory factory, object credentials)
        {
            var client = factory.CreateClient();
            var login = await client.PostAsJsonAsync("/api/web/login", credentials);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = (await login.ReadEnvelopeAsync<AuthData>()).Data!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private sealed class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
        {
            private readonly ConcurrentDictionary<string, GoogleIdentity> _tokens = new();

            public FakeGoogleIdTokenValidator(bool configured) => IsConfigured = configured;

            public bool IsConfigured { get; }

            public void Add(string idToken, GoogleIdentity identity) => _tokens[idToken] = identity;

            public Task<GoogleIdentity?> ValidateAsync(string idToken) =>
                Task.FromResult(_tokens.TryGetValue(idToken, out var identity) ? identity : null);
        }

        private sealed class CapturedDeliveries : IVerificationCodeSender
        {
            private readonly ConcurrentQueue<VerificationCodeMessage> _messages = new();

            public Task SendAsync(VerificationCodeMessage message)
            {
                _messages.Enqueue(message);
                return Task.CompletedTask;
            }

            public async Task<VerificationCodeMessage> WaitForAsync(string destination)
            {
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                while (DateTime.UtcNow < deadline)
                {
                    var found = _messages.LastOrDefault(m => m.Destination == destination);
                    if (found != null)
                        return found;
                    await Task.Delay(25);
                }

                throw new TimeoutException($"Nenhum código enviado para {destination}.");
            }
        }

        private sealed class GoogleApiFactory : FamilyVaultApiFactory
        {
            public GoogleApiFactory(string? adminEmail = null, bool configured = true)
                : base(adminEmail, adminEmail == null ? null : Password)
            {
                Google = new FakeGoogleIdTokenValidator(configured);
            }

            public FakeGoogleIdTokenValidator Google { get; }

            public CapturedDeliveries Deliveries { get; } = new();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IGoogleIdTokenValidator>();
                    services.AddSingleton<IGoogleIdTokenValidator>(Google);
                    services.AddKeyedSingleton<IVerificationCodeSender>(VerificationCodeQueue.SmsDeliveryKey, Deliveries);
                    services.AddKeyedSingleton<IVerificationCodeSender>(VerificationCodeQueue.EmailDeliveryKey, Deliveries);
                });
            }

            public async Task<(User User, IList<string> Roles, bool HasPassword)> InspectAsync(string userId)
            {
                using var scope = Services.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = (await userManager.FindByIdAsync(userId))!;
                return (user, await userManager.GetRolesAsync(user), await userManager.HasPasswordAsync(user));
            }

            // Grava o e-mail direto, como estaria num banco com duplicidade antiga
            public async Task SetEmailAsync(string userId, string email)
            {
                using var scope = Services.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = (await userManager.FindByIdAsync(userId))!;
                user.Email = email;
                user.NormalizedEmail = userManager.NormalizeEmail(email);
                await userManager.UpdateAsync(user);
            }

            public async Task<bool> HasGoogleLoginAsync(string sub)
            {
                using var scope = Services.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                return await userManager.FindByLoginAsync("Google", sub) != null;
            }
        }
    }
}
