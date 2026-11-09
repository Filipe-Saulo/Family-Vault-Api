using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyVaultApi.IntegrationTests.Http
{
    public class PasswordRecoveryTests : IClassFixture<ApiFixture>
    {
        private const string Password = "Senha@User123";
        private const string NewPassword = "Outra@Senha456";
        private const string InvalidCodeMessage = "Código inválido ou expirado.";

        private readonly ApiFixture _api;

        public PasswordRecoveryTests(ApiFixture api) => _api = api;

        [Fact]
        public async Task Forgot_WhenPhoneLimitIsReached_ShouldKeepAnswering202()
        {
            var (_, phone) = await _api.NewUserAsync();
            var client = _api.AnonymousClient();

            var first = await client.PostAsJsonAsync("/api/password/forgot", new { phone });
            var second = await client.PostAsJsonAsync("/api/password/forgot", new { phone });

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task Forgot_WhenAccountDoesNotExist_ShouldAnswer202WithNeutralMessage()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/password/forgot", new { email = "ninguem@exemplo.com" });

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await response.ReadEnvelopeAsync<object>()).Message.Should().Be("Se o telefone ou e-mail estiver cadastrado, enviaremos um código.");
        }

        [Fact]
        public async Task RegisterCode_WhenBothOrNeitherDestinationIsInformed_ShouldAnswer400()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();

            var both = await client.PostAsJsonAsync("/api/register/code", new { phoneNumber = "11987654312", email = "a@exemplo.com" });
            var neither = await client.PostAsJsonAsync("/api/register/code", new { });

            both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Forgot_WhenBothOrNeitherDestinationIsInformed_ShouldAnswer400()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();

            var both = await client.PostAsJsonAsync("/api/password/forgot", new { phone = "11987654312", email = "a@exemplo.com" });
            var neither = await client.PostAsJsonAsync("/api/password/forgot", new { });

            both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_ByPhoneWithDeliveredCode_ShouldCreateUserThatCanLogin()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string phone = "+5511987650101";

            var codeResponse = await client.PostAsJsonAsync("/api/register/code", new { phoneNumber = phone });
            var message = await factory.Deliveries.WaitForAsync(phone);
            var register = await client.PostAsJsonAsync("/api/register", RegisterBody(new { phoneNumber = phone }, message.Code));
            var login = await client.PostAsJsonAsync("/api/app/login", new { phone, password = Password });

            codeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
            message.Channel.Should().Be(VerificationChannel.Sms);
            message.Purpose.Should().Be(VerificationPurpose.Registration);
            message.Code.Should().MatchRegex("^[0-9]{6}$");
            register.StatusCode.Should().Be(HttpStatusCode.Created);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Register_ByEmailWithDeliveredCode_ShouldCreateUserThatLogsInWithEmailClaim()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";

            await client.PostAsJsonAsync("/api/register/code", new { email = "  Fulano@Exemplo.com " });
            var message = await factory.Deliveries.WaitForAsync(email);
            var register = await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, message.Code));
            var login = await client.PostAsJsonAsync("/api/app/login", new { email, password = Password });

            message.Channel.Should().Be(VerificationChannel.Email);
            register.StatusCode.Should().Be(HttpStatusCode.Created);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = (await login.ReadEnvelopeAsync<AuthResult>()).Data!.Token;
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
            claims.Should().Contain(c => c.Type == "email" && c.Value == email);
            claims.Should().NotContain(c => c.Type == "phone_number");
        }

        [Fact]
        public async Task Register_WhenCodeIsWrong_ShouldAnswer400AndNotCreateUser()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await client.PostAsJsonAsync("/api/register/code", new { email });
            var message = await factory.Deliveries.WaitForAsync(email);
            var wrong = message.Code == "000000" ? "111111" : "000000";

            var register = await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, wrong));
            var login = await client.PostAsJsonAsync("/api/app/login", new { email, password = Password });

            register.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await register.ReadEnvelopeAsync<object>()).Message.Should().Be(InvalidCodeMessage);
            login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Register_WhenPasswordIsRejected_ShouldKeepTheCodeForANewAttempt()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await client.PostAsJsonAsync("/api/register/code", new { email });
            var message = await factory.Deliveries.WaitForAsync(email);

            var weak = await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, message.Code, "123"));
            var strong = await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, message.Code));

            weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            strong.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Register_WhenCodeWasAlreadyUsed_ShouldAnswer400()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await client.PostAsJsonAsync("/api/register/code", new { email });
            var message = await factory.Deliveries.WaitForAsync(email);
            await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, message.Code));

            var again = await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, message.Code));

            again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task RegisterCode_WhenAccountAlreadyExists_ShouldAnswer202WithoutNewDelivery()
        {
            using var factory = new CapturingApiFactory(minIntervalSeconds: 0);
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await RegisterByEmailAsync(factory, client, email);

            var response = await client.PostAsJsonAsync("/api/register/code", new { email });
            await factory.Deliveries.EnsureNothingElseAfterFlushAsync(client);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            factory.Deliveries.To(email).Should().HaveCount(1);
        }

        [Fact]
        public async Task RegisterCode_WhenFormatIsInvalid_ShouldAnswer202WithoutDelivery()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/register/code", new { email = "nao-e-email" });
            await factory.Deliveries.EnsureNothingElseAfterFlushAsync(client);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            factory.Deliveries.All.Should().OnlyContain(m => m.Destination == CapturingApiFactory.FlushDestination);
        }

        [Fact]
        public async Task RegisterCode_WhenMinuteLimitIsReached_ShouldAnswer202WithoutSecondDelivery()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string phone = "+5511987650102";

            var first = await client.PostAsJsonAsync("/api/register/code", new { phoneNumber = phone });
            var second = await client.PostAsJsonAsync("/api/register/code", new { phoneNumber = phone });
            await factory.Deliveries.EnsureNothingElseAfterFlushAsync(client);

            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            second.StatusCode.Should().Be(HttpStatusCode.Accepted);
            factory.Deliveries.To(phone).Should().HaveCount(1);
        }

        [Fact]
        public async Task Forgot_WhenRegistrationCodeWasJustSentToTheSameEmail_ShouldShareTheLimitAndNotDeliver()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await RegisterByEmailAsync(factory, client, email);

            var forgot = await client.PostAsJsonAsync("/api/password/forgot", new { email });
            await factory.Deliveries.EnsureNothingElseAfterFlushAsync(client);

            forgot.StatusCode.Should().Be(HttpStatusCode.Accepted);
            factory.Deliveries.To(email).Should().HaveCount(1);
        }

        [Fact]
        public async Task ForgotAndReset_ByEmail_ShouldChangePasswordWithDeliveredCode()
        {
            using var factory = new CapturingApiFactory(minIntervalSeconds: 0);
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await RegisterByEmailAsync(factory, client, email);

            var forgot = await client.PostAsJsonAsync("/api/password/forgot", new { email });
            var message = await factory.Deliveries.WaitForAsync(email, VerificationPurpose.PasswordReset);
            var reset = await client.PostAsJsonAsync("/api/password/reset", new { email, code = message.Code, password = NewPassword, passwordConfirm = NewPassword });
            var oldLogin = await client.PostAsJsonAsync("/api/app/login", new { email, password = Password });
            var newLogin = await client.PostAsJsonAsync("/api/app/login", new { email, password = NewPassword });

            forgot.StatusCode.Should().Be(HttpStatusCode.Accepted);
            message.Channel.Should().Be(VerificationChannel.Email);
            reset.StatusCode.Should().Be(HttpStatusCode.OK);
            oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Reset_ByEmailWithWrongCode_ShouldAnswer400AndKeepOldPassword()
        {
            using var factory = new CapturingApiFactory(minIntervalSeconds: 0);
            var client = factory.CreateClient();
            const string email = "fulano@exemplo.com";
            await RegisterByEmailAsync(factory, client, email);
            await client.PostAsJsonAsync("/api/password/forgot", new { email });
            var message = await factory.Deliveries.WaitForAsync(email, VerificationPurpose.PasswordReset);
            var wrong = message.Code == "000000" ? "111111" : "000000";

            var reset = await client.PostAsJsonAsync("/api/password/reset", new { email, code = wrong, password = NewPassword, passwordConfirm = NewPassword });
            var login = await client.PostAsJsonAsync("/api/app/login", new { email, password = Password });

            reset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await reset.ReadEnvelopeAsync<object>()).Message.Should().Be(InvalidCodeMessage);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Login_WhenEmailHasUppercaseAndSpaces_ShouldFindTheAccount()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            await RegisterByEmailAsync(factory, client, "fulano@exemplo.com");

            var login = await client.PostAsJsonAsync("/api/app/login", new { email = "  Fulano@Exemplo.COM ", password = Password });

            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ForgotAndReset_WhenAdministratorEmailWasSavedWithUppercase_ShouldDeliverAndChangePassword()
        {
            using var factory = new CapturingApiFactory(adminEmail: "Admin@Exemplo.com");
            var client = factory.CreateClient();
            const string email = "admin@exemplo.com";

            await client.PostAsJsonAsync("/api/password/forgot", new { email });
            var message = await factory.Deliveries.WaitForAsync(email, VerificationPurpose.PasswordReset);
            var reset = await client.PostAsJsonAsync("/api/password/reset", new { email, code = message.Code, password = NewPassword, passwordConfirm = NewPassword });
            var login = await client.PostAsJsonAsync("/api/app/login", new { email = "Admin@Exemplo.com", password = NewPassword });

            reset.StatusCode.Should().Be(HttpStatusCode.OK);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Forgot_WhenEmailIsNotTheUserNameOfAnyAccount_ShouldNotDeliver()
        {
            using var factory = new CapturingApiFactory();
            var client = factory.CreateClient();
            const string phone = "+5511987650103";
            const string otherEmail = "opcional@exemplo.com";
            await client.PostAsJsonAsync("/api/register/code", new { phoneNumber = phone });
            var message = await factory.Deliveries.WaitForAsync(phone);
            (await client.PostAsJsonAsync("/api/register", RegisterBody(new { phoneNumber = phone }, message.Code))).StatusCode.Should().Be(HttpStatusCode.Created);

            var forgot = await client.PostAsJsonAsync("/api/password/forgot", new { email = otherEmail });
            await factory.Deliveries.EnsureNothingElseAfterFlushAsync(client);

            forgot.StatusCode.Should().Be(HttpStatusCode.Accepted);
            factory.Deliveries.To(otherEmail).Should().BeEmpty();
        }

        [Fact]
        public void Startup_OutsideDevelopmentWithoutTwilioCredentials_ShouldFail()
        {
            using var factory = new ProductionApiFactory(twilio: false, resend: true);

            Action act = () => factory.CreateClient();

            act.Should().Throw<InvalidOperationException>().WithMessage("*Twilio*");
        }

        [Fact]
        public void Startup_OutsideDevelopmentWithTwilioButWithoutResendCredentials_ShouldFail()
        {
            using var factory = new ProductionApiFactory(twilio: true, resend: false);

            Action act = () => factory.CreateClient();

            act.Should().Throw<InvalidOperationException>().WithMessage("*Resend*");
        }

        private static object RegisterBody(object destination, string code, string password = Password)
        {
            var body = new Dictionary<string, object>
            {
                ["code"] = code,
                ["password"] = password,
                ["passwordConfirm"] = password,
                ["firstName"] = "Fulano",
                ["lastName"] = "Silva",
                ["age"] = 30
            };
            foreach (var property in destination.GetType().GetProperties())
                body[property.Name] = property.GetValue(destination)!;
            return body;
        }

        private static async Task RegisterByEmailAsync(CapturingApiFactory factory, HttpClient client, string email)
        {
            await client.PostAsJsonAsync("/api/register/code", new { email });
            var message = await factory.Deliveries.WaitForAsync(email, VerificationPurpose.Registration);
            var register = await client.PostAsJsonAsync("/api/register", RegisterBody(new { email }, message.Code));
            register.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        private sealed record AuthResult(string UserId, string Token, string? RefreshToken);

        // Guarda o que o dispatcher entregaria; é o lugar onde o teste lê o código
        private sealed class CapturedDeliveries : IVerificationCodeSender
        {
            private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
            private readonly ConcurrentQueue<VerificationCodeMessage> _messages = new();

            public IReadOnlyList<VerificationCodeMessage> All => _messages.ToList();

            public IReadOnlyList<VerificationCodeMessage> To(string destination) => _messages.Where(m => m.Destination == destination).ToList();

            public Task SendAsync(VerificationCodeMessage message)
            {
                _messages.Enqueue(message);
                return Task.CompletedTask;
            }

            public async Task<VerificationCodeMessage> WaitForAsync(string destination, VerificationPurpose? purpose = null)
            {
                var deadline = DateTime.UtcNow + Timeout;
                while (DateTime.UtcNow < deadline)
                {
                    var found = _messages.LastOrDefault(m => m.Destination == destination && (purpose == null || m.Purpose == purpose));
                    if (found != null)
                        return found;
                    await Task.Delay(25);
                }

                throw new TimeoutException($"Nenhum código entregue para {destination}.");
            }

            // A fila é FIFO: depois que a entrega de outro destino chega, tudo o que foi pedido antes já foi processado
            public async Task EnsureNothingElseAfterFlushAsync(HttpClient client)
            {
                var response = await client.PostAsJsonAsync("/api/register/code", new { email = CapturingApiFactory.FlushDestination });
                response.StatusCode.Should().Be(HttpStatusCode.Accepted);
                await WaitForAsync(CapturingApiFactory.FlushDestination);
            }
        }

        // Uma factory por teste: o rate limit por IP (3/min em /register/code e /password/forgot, 5/min em /register e login) é por factory
        private sealed class CapturingApiFactory : FamilyVaultApiFactory
        {
            public const string FlushDestination = "flush@exemplo.com";

            private readonly int? _minIntervalSeconds;

            public CapturingApiFactory(int? minIntervalSeconds = null, string? adminEmail = null) : base(adminEmail, adminEmail == null ? null : Password)
            {
                _minIntervalSeconds = minIntervalSeconds;
            }

            public CapturedDeliveries Deliveries { get; } = new();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.UseSetting("VerificationCode:SendLimit:PerDay", "5");
                if (_minIntervalSeconds != null)
                    builder.UseSetting("VerificationCode:SendLimit:MinIntervalSeconds", _minIntervalSeconds.Value.ToString());

                builder.ConfigureServices(services =>
                {
                    services.AddKeyedSingleton<IVerificationCodeSender>(VerificationCodeQueue.SmsDeliveryKey, Deliveries);
                    services.AddKeyedSingleton<IVerificationCodeSender>(VerificationCodeQueue.EmailDeliveryKey, Deliveries);
                });
            }
        }

        private sealed class ProductionApiFactory : FamilyVaultApiFactory
        {
            private readonly bool _twilio;
            private readonly bool _resend;

            public ProductionApiFactory(bool twilio, bool resend) : base(null, null)
            {
                _twilio = twilio;
                _resend = resend;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.UseEnvironment("Production");

                if (_twilio)
                {
                    builder.UseSetting("Sms:Twilio:AccountSid", "AC123");
                    builder.UseSetting("Sms:Twilio:AuthToken", "secret");
                    builder.UseSetting("Sms:Twilio:From", "+15005550006");
                }

                if (_resend)
                {
                    builder.UseSetting("Email:Resend:ApiKey", "re_secret");
                    builder.UseSetting("Email:Resend:From", "Family Vault <nao-responda@exemplo.com>");
                }
            }
        }
    }
}
