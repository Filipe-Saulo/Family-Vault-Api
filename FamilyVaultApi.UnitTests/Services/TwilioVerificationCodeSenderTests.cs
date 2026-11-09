using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;

namespace FamilyVaultApi.UnitTests.Services
{
    public class TwilioVerificationCodeSenderTests
    {
        private const string Phone = "+5511987654312";
        private const string Code = "654321";

        private static VerificationCodeMessage Message(VerificationPurpose purpose = VerificationPurpose.PasswordReset) =>
            new(VerificationChannel.Sms, Phone, Code, purpose);

        private static TwilioVerificationCodeSender CreateSender(StubHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(TwilioVerificationCodeSender.HttpClientName)).Returns(new HttpClient(handler));

            return new TwilioVerificationCodeSender(factory.Object, Options.Create(new TwilioSmsOptions
            {
                AccountSid = "AC123",
                AuthToken = "secret",
                From = "+15005550006"
            }));
        }

        [Fact]
        public async Task SendAsync_WhenProviderAccepts_ShouldPostMessageWithBasicAuth()
        {
            var handler = new StubHandler(HttpStatusCode.Created, "{}");

            await CreateSender(handler).SendAsync(Message());

            handler.Method.Should().Be(HttpMethod.Post);
            handler.Url.Should().Be("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages.json");
            handler.Scheme.Should().Be("Basic");
            handler.Credentials.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("AC123:secret")));
            handler.Form["To"].Should().Be(Phone);
            handler.Form["From"].Should().Be("+15005550006");
            handler.Form["Body"].Should().Be("Family Vault: seu código de recuperação é 654321. Não compartilhe.");
        }

        [Fact]
        public async Task SendAsync_WhenPurposeIsRegistration_ShouldUseRegistrationText()
        {
            var handler = new StubHandler(HttpStatusCode.Created, "{}");

            await CreateSender(handler).SendAsync(Message(VerificationPurpose.Registration));

            handler.Form["Body"].Should().Be("Family Vault: seu código de cadastro é 654321. Não compartilhe.");
        }

        [Fact]
        public async Task SendAsync_WhenProviderRejects_ShouldThrowWithCodesButNotThePhoneOrTheVerificationCode()
        {
            var handler = new StubHandler(HttpStatusCode.BadRequest, $"{{\"code\":21608,\"message\":\"The number {Phone} is unverified\"}}");

            var act = () => CreateSender(handler).SendAsync(Message());

            var error = await act.Should().ThrowAsync<HttpRequestException>();
            error.Which.Message.Should().Contain("400").And.Contain("21608").And.NotContain(Phone).And.NotContain(Code);
        }

        [Fact]
        public async Task SendAsync_WhenErrorBodyIsNotJson_ShouldStillThrowHttpRequestException()
        {
            var handler = new StubHandler(HttpStatusCode.BadGateway, "<html>bad gateway</html>");

            var act = () => CreateSender(handler).SendAsync(Message());

            await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*502*desconhecido*");
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
            public Dictionary<string, string> Form { get; private set; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Method = request.Method;
                Url = request.RequestUri!.ToString();
                Scheme = request.Headers.Authorization!.Scheme;
                Credentials = request.Headers.Authorization.Parameter;
                Form = (await request.Content!.ReadAsStringAsync(cancellationToken))
                    .Split('&')
                    .Select(pair => pair.Split('='))
                    .ToDictionary(pair => pair[0], pair => WebUtility.UrlDecode(pair[1]));

                return new HttpResponseMessage(_status) { Content = new StringContent(_responseBody) };
            }
        }
    }
}
