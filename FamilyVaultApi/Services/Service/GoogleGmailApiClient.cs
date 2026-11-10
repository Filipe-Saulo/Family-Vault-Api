using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using GoogleHttpClientFactory = Google.Apis.Http.IHttpClientFactory;

namespace FamilyVaultApi.Services.Service
{
    // Cliente real, pelo SDK oficial (Google.Apis.Gmail.v1 e Google.Apis.Auth). Nada é guardado pelo SDK
    // (DataStore nulo): o refresh token vive só criptografado no banco.
    public class GoogleGmailApiClient : IGmailApiClient
    {
        private const string FlowUserId = "family-vault";
        private const int MaxPageSize = 500;

        private readonly GmailOptions _options;
        private readonly GoogleHttpClientFactory _httpClientFactory;
        private readonly GoogleAuthorizationCodeFlow _flow;

        public GoogleGmailApiClient(IOptions<GmailOptions> options, GoogleHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = _options.ClientId, ClientSecret = _options.ClientSecret },
                Scopes = new[] { GmailService.Scope.GmailReadonly },
                HttpClientFactory = httpClientFactory
            });
        }

        public string BuildAuthorizationUrl(string state)
        {
            var request = (GoogleAuthorizationCodeRequestUrl)_flow.CreateAuthorizationCodeRequest(_options.RedirectUri);
            request.State = state;
            // offline + consent: o Google só manda refresh token na primeira autorização, a não ser que peça o consentimento de novo
            request.AccessType = "offline";
            request.Prompt = "consent";
            return request.Build().AbsoluteUri;
        }

        public async Task<GmailAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
        {
            var token = await _flow.ExchangeCodeForTokenAsync(FlowUserId, code, _options.RedirectUri, cancellationToken);

            var scopes = (token.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new GmailAuthorization
            {
                RefreshToken = token.RefreshToken,
                HasGmailReadScope = scopes.Contains(GmailService.Scope.GmailReadonly, StringComparer.Ordinal)
            };

            if (!result.HasGmailReadScope)
                return result;

            using var service = CreateService(token.AccessToken);
            var profile = await service.Users.GetProfile("me").ExecuteAsync(cancellationToken);
            result.GoogleEmail = profile.EmailAddress?.Trim().ToLowerInvariant() ?? string.Empty;

            return result;
        }

        public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
        {
            await _flow.RevokeTokenAsync(FlowUserId, refreshToken, cancellationToken);
        }

        public async Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var token = await _flow.RefreshTokenAsync(FlowUserId, refreshToken, cancellationToken);
                return token.AccessToken;
            }
            catch (TokenResponseException ex) when (ex.Error?.Error == "invalid_grant")
            {
                // Senha do Google trocada, 6 meses sem uso ou acesso revogado pela pessoa
                throw new GmailAccessRevokedException();
            }
        }

        public async Task<List<string>> ListMessageIdsAsync(string accessToken, string query, int maxResults, CancellationToken cancellationToken)
        {
            using var service = CreateService(accessToken);
            var ids = new List<string>();
            string? pageToken = null;

            do
            {
                var request = service.Users.Messages.List("me");
                request.Q = query;
                request.MaxResults = Math.Min(MaxPageSize, maxResults - ids.Count);
                request.PageToken = pageToken;

                var response = await ExecuteAsync(() => request.ExecuteAsync(cancellationToken));
                ids.AddRange((response.Messages ?? new List<Message>()).Select(m => m.Id));
                pageToken = response.NextPageToken;
            }
            while (pageToken != null && ids.Count < maxResults);

            return ids.Take(maxResults).ToList();
        }

        public async Task<GmailMessageInfo> GetMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken)
        {
            var message = await GetFullMessageAsync(accessToken, messageId, cancellationToken);

            var from = message.Payload?.Headers?
                .FirstOrDefault(h => string.Equals(h.Name, "From", StringComparison.OrdinalIgnoreCase))?.Value;

            return new GmailMessageInfo
            {
                MessageId = message.Id,
                Sender = ParseAddress(from),
                ReceivedAt = message.InternalDate.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value).UtcDateTime
                    : DateTime.UtcNow,
                PdfAttachments = Flatten(message.Payload)
                    .Where(IsPdf)
                    .Select(p => new GmailPdfAttachment { PartId = p.PartId, Size = p.Body?.Size ?? 0 })
                    .ToList()
            };
        }

        public async Task<byte[]> GetAttachmentAsync(string accessToken, string messageId, string partId, CancellationToken cancellationToken)
        {
            var message = await GetFullMessageAsync(accessToken, messageId, cancellationToken);

            var part = Flatten(message.Payload).FirstOrDefault(p => p.PartId == partId && IsPdf(p))
                ?? throw new GmailMessageNotFoundException();

            // Anexo pequeno pode vir dentro da própria mensagem
            if (!string.IsNullOrEmpty(part.Body?.Data))
                return DecodeBase64Url(part.Body.Data);

            using var service = CreateService(accessToken);
            var body = await ExecuteAsync(() => service.Users.Messages.Attachments.Get("me", messageId, part.Body!.AttachmentId).ExecuteAsync(cancellationToken));

            return DecodeBase64Url(body.Data ?? string.Empty);
        }

        private async Task<Message> GetFullMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken)
        {
            using var service = CreateService(accessToken);
            var request = service.Users.Messages.Get("me", messageId);
            request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

            return await ExecuteAsync(() => request.ExecuteAsync(cancellationToken));
        }

        private GmailService CreateService(string accessToken) => new(new BaseClientService.Initializer
        {
            HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
            HttpClientFactory = _httpClientFactory,
            ApplicationName = "family-vault-api"
        });

        private static async Task<T> ExecuteAsync<T>(Func<Task<T>> call)
        {
            try
            {
                return await call();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                throw new GmailMessageNotFoundException();
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                // O access token acabou de ser emitido: 401 aqui é acesso retirado
                throw new GmailAccessRevokedException();
            }
        }

        private static bool IsPdf(MessagePart part) =>
            (string.Equals(part.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                || (part.Filename?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false))
            && (!string.IsNullOrEmpty(part.Body?.AttachmentId) || !string.IsNullOrEmpty(part.Body?.Data));

        private static IEnumerable<MessagePart> Flatten(MessagePart? part)
        {
            if (part == null)
                yield break;

            yield return part;

            foreach (var child in part.Parts ?? new List<MessagePart>())
                foreach (var descendant in Flatten(child))
                    yield return descendant;
        }

        public static string? ParseAddress(string? from)
        {
            if (string.IsNullOrWhiteSpace(from))
                return null;

            try
            {
                return new MailAddress(from).Address.Trim().ToLowerInvariant();
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public static byte[] DecodeBase64Url(string data)
        {
            var base64 = data.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return Convert.FromBase64String(base64);
        }
    }
}
