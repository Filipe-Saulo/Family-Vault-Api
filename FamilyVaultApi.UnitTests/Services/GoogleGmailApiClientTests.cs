using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Google.Apis.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace FamilyVaultApi.UnitTests.Services
{
    // O SDK do Google de verdade, com as respostas HTTP simuladas: nenhuma chamada sai para o Google
    public class GoogleGmailApiClientTests
    {
        private const string GmailScope = "https://www.googleapis.com/auth/gmail.readonly";
        private const string RedirectUri = "https://api.exemplo.test/api/gmail/callback";

        private readonly RoutingHandler _handler = new();
        private readonly GoogleGmailApiClient _client;

        public GoogleGmailApiClientTests()
        {
            _client = new GoogleGmailApiClient(Options.Create(new GmailOptions
            {
                ClientId = "client-id.apps.googleusercontent.com",
                ClientSecret = "client-secret",
                RedirectUri = RedirectUri
            }), new StubFactory(_handler));
        }

        [Fact]
        public void BuildAuthorizationUrl_ShouldAskOfflineGmailReadonlyWithConsentAndState()
        {
            var url = new Uri(_client.BuildAuthorizationUrl("estado-123"));

            var query = QueryHelpers.ParseQuery(url.Query);
            url.Host.Should().Be("accounts.google.com");
            query["client_id"].ToString().Should().Be("client-id.apps.googleusercontent.com");
            query["redirect_uri"].ToString().Should().Be(RedirectUri);
            query["scope"].ToString().Should().Be(GmailScope);
            query["response_type"].ToString().Should().Be("code");
            query["access_type"].ToString().Should().Be("offline");
            query["prompt"].ToString().Should().Be("consent");
            query["state"].ToString().Should().Be("estado-123");
        }

        [Fact]
        public async Task ExchangeCodeAsync_ShouldPostTheCodeAndReadTheAccountEmail()
        {
            _handler.On(HttpMethod.Post, "oauth2.googleapis.com/token", HttpStatusCode.OK,
                $"{{\"access_token\":\"at-1\",\"refresh_token\":\"rt-1\",\"scope\":\"{GmailScope}\",\"expires_in\":3599,\"token_type\":\"Bearer\"}}");
            _handler.On(HttpMethod.Get, "/gmail/v1/users/me/profile", HttpStatusCode.OK, "{\"emailAddress\":\"Pessoa@Gmail.com\"}");

            var result = await _client.ExchangeCodeAsync("code-1", CancellationToken.None);

            result.RefreshToken.Should().Be("rt-1");
            result.HasGmailReadScope.Should().BeTrue();
            result.GoogleEmail.Should().Be("pessoa@gmail.com");
            var form = QueryHelpers.ParseQuery(_handler.BodyOf("oauth2.googleapis.com/token"));
            form["code"].ToString().Should().Be("code-1");
            form["redirect_uri"].ToString().Should().Be(RedirectUri);
            form["grant_type"].ToString().Should().Be("authorization_code");
            _handler.AuthorizationOf("/gmail/v1/users/me/profile").Should().Be("Bearer at-1");
        }

        [Fact]
        public async Task ExchangeCodeAsync_WithoutGmailScope_ShouldNotReadTheProfile()
        {
            _handler.On(HttpMethod.Post, "oauth2.googleapis.com/token", HttpStatusCode.OK,
                "{\"access_token\":\"at-1\",\"refresh_token\":\"rt-1\",\"scope\":\"openid\",\"expires_in\":3599}");

            var result = await _client.ExchangeCodeAsync("code-1", CancellationToken.None);

            result.HasGmailReadScope.Should().BeFalse();
            _handler.Requests.Should().NotContain(r => r.Contains("/profile"));
        }

        [Fact]
        public async Task GetAccessTokenAsync_InvalidGrant_ShouldThrowRevoked()
        {
            _handler.On(HttpMethod.Post, "oauth2.googleapis.com/token", HttpStatusCode.BadRequest,
                "{\"error\":\"invalid_grant\",\"error_description\":\"Token has been expired or revoked.\"}");

            Func<Task> act = () => _client.GetAccessTokenAsync("rt-1", CancellationToken.None);

            await act.Should().ThrowAsync<GmailAccessRevokedException>();
        }

        [Fact]
        public async Task GetAccessTokenAsync_ShouldUseTheRefreshTokenGrant()
        {
            _handler.On(HttpMethod.Post, "oauth2.googleapis.com/token", HttpStatusCode.OK, "{\"access_token\":\"at-2\",\"expires_in\":3599}");

            var token = await _client.GetAccessTokenAsync("rt-1", CancellationToken.None);

            token.Should().Be("at-2");
            var form = QueryHelpers.ParseQuery(_handler.BodyOf("oauth2.googleapis.com/token"));
            form["grant_type"].ToString().Should().Be("refresh_token");
            form["refresh_token"].ToString().Should().Be("rt-1");
        }

        [Fact]
        public async Task ListMessageIdsAsync_ShouldFollowPagesAndSendTheQuery()
        {
            _handler.OnSequence(HttpMethod.Get, "/gmail/v1/users/me/messages?",
                "{\"messages\":[{\"id\":\"a\"},{\"id\":\"b\"}],\"nextPageToken\":\"p2\"}",
                "{\"messages\":[{\"id\":\"c\"}]}");

            var ids = await _client.ListMessageIdsAsync("at-1", "has:attachment filename:pdf", 10, CancellationToken.None);

            ids.Should().Equal("a", "b", "c");
            var first = new Uri(_handler.Requests.First(r => r.Contains("/messages?")));
            QueryHelpers.ParseQuery(first.Query)["q"].ToString().Should().Be("has:attachment filename:pdf");
            _handler.Requests.Should().Contain(r => r.Contains("pageToken=p2"));
        }

        [Fact]
        public async Task GetMessageAsync_ShouldReadSenderDateAndOnlyThePdfParts()
        {
            _handler.On(HttpMethod.Get, "/gmail/v1/users/me/messages/m1", HttpStatusCode.OK, MessageJson);

            var message = await _client.GetMessageAsync("at-1", "m1", CancellationToken.None);

            message.MessageId.Should().Be("m1");
            message.Sender.Should().Be("cobranca@energia.test");
            message.ReceivedAt.Should().Be(new DateTime(2026, 11, 10, 16, 0, 0, DateTimeKind.Utc));
            message.PdfAttachments.Select(a => a.PartId).Should().Equal("1", "2");
            message.PdfAttachments[0].Size.Should().Be(1234);
        }

        [Fact]
        public async Task GetMessageAsync_DeletedMessage_ShouldThrowNotFound()
        {
            _handler.On(HttpMethod.Get, "/gmail/v1/users/me/messages/gone", HttpStatusCode.NotFound,
                "{\"error\":{\"code\":404,\"message\":\"Requested entity was not found.\",\"status\":\"NOT_FOUND\"}}");

            Func<Task> act = () => _client.GetMessageAsync("at-1", "gone", CancellationToken.None);

            await act.Should().ThrowAsync<GmailMessageNotFoundException>();
        }

        [Fact]
        public async Task GetAttachmentAsync_ShouldFindTheCurrentAttachmentIdByPartIdAndDecodeBase64Url()
        {
            _handler.On(HttpMethod.Get, "/gmail/v1/users/me/messages/m1", HttpStatusCode.OK, MessageJson);
            _handler.On(HttpMethod.Get, "/gmail/v1/users/me/messages/m1/attachments/att-x", HttpStatusCode.OK, "{\"data\":\"JVBERi0x-_8\",\"size\":9}");

            var content = await _client.GetAttachmentAsync("at-1", "m1", "1", CancellationToken.None);

            content.Should().Equal(Convert.FromBase64String("JVBERi0x+/8="));
        }

        [Fact]
        public async Task GetAttachmentAsync_UnknownPart_ShouldThrowNotFound()
        {
            _handler.On(HttpMethod.Get, "/gmail/v1/users/me/messages/m1", HttpStatusCode.OK, MessageJson);

            Func<Task> act = () => _client.GetAttachmentAsync("at-1", "m1", "3", CancellationToken.None);

            await act.Should().ThrowAsync<GmailMessageNotFoundException>();
        }

        [Fact]
        public async Task RevokeAsync_ShouldCallTheGoogleRevokeEndpoint()
        {
            _handler.On(null, "oauth2.googleapis.com/revoke", HttpStatusCode.OK, "{}");

            await _client.RevokeAsync("rt-1", CancellationToken.None);

            _handler.Requests.Should().ContainSingle(r => r.Contains("/revoke") && r.Contains("token=rt-1"));
        }

        [Theory]
        [InlineData("Energia SA <Cobranca@Energia.test>", "cobranca@energia.test")]
        [InlineData("fatura@banco.test", "fatura@banco.test")]
        [InlineData("sem endereço", null)]
        [InlineData(null, null)]
        public void ParseAddress_ShouldReturnTheLowercaseAddress(string? from, string? expected)
        {
            GoogleGmailApiClient.ParseAddress(from).Should().Be(expected);
        }

        // 1794326400000 = 2026-11-10T16:00:00Z. Partes: texto, PDF pelo tipo, PDF pela extensão, imagem.
        private const string MessageJson = """
            {
              "id": "m1",
              "internalDate": "1794326400000",
              "payload": {
                "partId": "",
                "mimeType": "multipart/mixed",
                "headers": [ { "name": "From", "value": "Energia SA <Cobranca@Energia.test>" }, { "name": "Subject", "value": "Fatura" } ],
                "parts": [
                  { "partId": "0", "mimeType": "text/plain", "filename": "", "body": { "size": 10, "data": "dGV4dG8" } },
                  { "partId": "1", "mimeType": "application/pdf", "filename": "fatura.pdf", "body": { "attachmentId": "att-x", "size": 1234 } },
                  { "partId": "2", "mimeType": "application/octet-stream", "filename": "boleto.PDF", "body": { "attachmentId": "att-y", "size": 20 } },
                  { "partId": "3", "mimeType": "image/png", "filename": "logo.png", "body": { "attachmentId": "att-z", "size": 5 } }
                ]
              }
            }
            """;

        private sealed class StubFactory : HttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public StubFactory(HttpMessageHandler handler) => _handler = handler;

            protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => _handler;
        }

        // Responde pelo método e por um trecho da URL; guarda as URLs, os corpos e o Authorization recebidos
        private sealed class RoutingHandler : HttpMessageHandler
        {
            private readonly List<(HttpMethod? Method, string Url, HttpStatusCode Status, Queue<string> Bodies)> _routes = new();
            private readonly Dictionary<string, string> _bodies = new();
            private readonly Dictionary<string, string?> _authorizations = new();

            public List<string> Requests { get; } = new();

            public void On(HttpMethod? method, string urlPart, HttpStatusCode status, string body) =>
                _routes.Add((method, urlPart, status, new Queue<string>(new[] { body })));

            public void OnSequence(HttpMethod method, string urlPart, params string[] bodies) =>
                _routes.Add((method, urlPart, HttpStatusCode.OK, new Queue<string>(bodies)));

            public string BodyOf(string urlPart) => _bodies.First(b => b.Key.Contains(urlPart)).Value;

            public string? AuthorizationOf(string urlPart) => _authorizations.First(a => a.Key.Contains(urlPart)).Value;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                Requests.Add(url);
                _bodies[url] = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                _authorizations[url] = request.Headers.Authorization?.ToString();

                // A rota mais específica (trecho mais longo) vence
                var route = _routes
                    .Where(r => (r.Method == null || r.Method == request.Method) && url.Contains(r.Url))
                    .OrderByDescending(r => r.Url.Length)
                    .FirstOrDefault();

                if (route.Url == null)
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };

                var body = route.Bodies.Count > 1 ? route.Bodies.Dequeue() : route.Bodies.Peek();
                return new HttpResponseMessage(route.Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }
    }
}
