using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text.Json;

namespace FamilyVaultApi.UnitTests.Services
{
    public class ResendVerificationCodeSenderTests
    {
        private const string Email = "fulano@exemplo.com";
        private const string Code = "654321";

        private static VerificationCodeMessage Message(VerificationPurpose purpose = VerificationPurpose.PasswordReset) =>
            new(VerificationChannel.Email, Email, Code, purpose);

        private static ResendVerificationCodeSender CreateSender(StubHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(ResendVerificationCodeSender.HttpClientName)).Returns(new HttpClient(handler));

            return new ResendVerificationCodeSender(factory.Object, Options.Create(new ResendEmailOptions
            {
                ApiKey = "re_secret",
                From = "Family Vault <nao-responda@exemplo.com>"
            }));
        }

        [Fact]
        public async Task SendAsync_WhenProviderAccepts_ShouldPostJsonWithBearerAuth()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");

            await CreateSender(handler).SendAsync(Message());

            handler.Method.Should().Be(HttpMethod.Post);
            handler.Url.Should().Be("https://api.resend.com/emails");
            handler.Scheme.Should().Be("Bearer");
            handler.Credentials.Should().Be("re_secret");
            handler.Json.GetProperty("from").GetString().Should().Be("Family Vault <nao-responda@exemplo.com>");
            handler.Json.GetProperty("to").EnumerateArray().Select(x => x.GetString()).Should().Equal(Email);
            handler.Json.GetProperty("subject").GetString().Should().Be("Seu código de recuperação do Family Vault");
            handler.Json.GetProperty("text").GetString().Should().Be("Family Vault: seu código de recuperação é 654321. Não compartilhe.");
        }

        [Fact]
        public async Task SendAsync_WhenPurposeIsRegistration_ShouldUseRegistrationSubjectAndText()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");

            await CreateSender(handler).SendAsync(Message(VerificationPurpose.Registration));

            handler.Json.GetProperty("subject").GetString().Should().Be("Seu código de cadastro do Family Vault");
            handler.Json.GetProperty("text").GetString().Should().Be("Family Vault: seu código de cadastro é 654321. Não compartilhe.");
        }

        [Fact]
        public async Task SendAsync_WhenProviderRejects_ShouldThrowWithStatusButNotTheEmailOrTheCode()
        {
            var handler = new StubHandler(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Invalid to " + Email + "\"}");

            var act = () => CreateSender(handler).SendAsync(Message());

            var error = await act.Should().ThrowAsync<HttpRequestException>();
            error.Which.Message.Should().Contain("422").And.NotContain(Email).And.NotContain(Code);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _responseBody;

            public StubHandler(HttpStatusCode status, string responseBody)
            {
                _status = status;
                _responseBody = responseBody;
            }

            public HttpMethod? Method { get; private set; }
            public string? Url { get; private set; }
            public string? Scheme { get; private set; }
            public string? Credentials { get; private set; }
            public JsonElement Json { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Method = request.Method;
                Url = request.RequestUri!.ToString();
                Scheme = request.Headers.Authorization!.Scheme;
                Credentials = request.Headers.Authorization.Parameter;
                Json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();

                return new HttpResponseMessage(_status) { Content = new StringContent(_responseBody) };
            }
        }
    }
}
