using System.Collections.Concurrent;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // Gmail falso: cada refresh token tem uma caixa em memória. Nenhuma chamada ao Google.
    // A busca não é filtrada aqui (a montagem da query tem teste unitário); a caixa devolve tudo, do mais novo ao mais antigo.
    public class FakeGmailApiClient : IGmailApiClient
    {
        private readonly ConcurrentDictionary<string, GmailAuthorization> _codes = new();
        private readonly ConcurrentDictionary<string, List<FakeMessage>> _mailboxes = new();
        private readonly ConcurrentDictionary<string, bool> _revokedGrants = new();
        private readonly ConcurrentBag<string> _revokeCalls = new();
        private readonly ConcurrentBag<string> _queries = new();

        public sealed record FakeMessage(string Id, string? Sender, DateTime ReceivedAt, List<(string PartId, byte[] Content, long Size)> Pdfs);

        // O próximo callback com este code autoriza a conta com este refresh token
        public void AddAuthorization(string code, string refreshToken, string email, bool hasGmailScope = true) =>
            _codes[code] = new GmailAuthorization { RefreshToken = refreshToken, GoogleEmail = email, HasGmailReadScope = hasGmailScope };

        public void AddMessage(string refreshToken, FakeMessage message)
        {
            var mailbox = _mailboxes.GetOrAdd(refreshToken, _ => new List<FakeMessage>());
            lock (mailbox) mailbox.Add(message);
        }

        public void RemoveMessage(string refreshToken, string messageId)
        {
            var mailbox = _mailboxes.GetOrAdd(refreshToken, _ => new List<FakeMessage>());
            lock (mailbox) mailbox.RemoveAll(m => m.Id == messageId);
        }

        // Simula senha do Google trocada / acesso retirado: o próximo refresh dá invalid_grant
        public void RevokeGrant(string refreshToken) => _revokedGrants[refreshToken] = true;

        public bool WasRevoked(string refreshToken) => _revokeCalls.Contains(refreshToken);

        public List<string> Queries => _queries.ToList();

        public string BuildAuthorizationUrl(string state) => $"https://accounts.google.test/o/oauth2/auth?state={Uri.EscapeDataString(state)}";

        public Task<GmailAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
            _codes.TryRemove(code, out var authorization)
                ? Task.FromResult(authorization)
                : throw new InvalidOperationException("code desconhecido");

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
        {
            _revokeCalls.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
            _revokedGrants.ContainsKey(refreshToken)
                ? throw new GmailAccessRevokedException()
                : Task.FromResult("access:" + refreshToken);

        public Task<List<string>> ListMessageIdsAsync(string accessToken, string query, int maxResults, CancellationToken cancellationToken)
        {
            _queries.Add(query);
            var mailbox = Mailbox(accessToken);
            lock (mailbox)
                return Task.FromResult(mailbox.OrderByDescending(m => m.ReceivedAt).Take(maxResults).Select(m => m.Id).ToList());
        }

        public Task<GmailMessageInfo> GetMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken)
        {
            var message = Find(accessToken, messageId);
            return Task.FromResult(new GmailMessageInfo
            {
                MessageId = message.Id,
                Sender = message.Sender,
                ReceivedAt = message.ReceivedAt,
                PdfAttachments = message.Pdfs.Select(p => new GmailPdfAttachment { PartId = p.PartId, Size = p.Size }).ToList()
            });
        }

        public Task<byte[]> GetAttachmentAsync(string accessToken, string messageId, string partId, CancellationToken cancellationToken)
        {
            var pdf = Find(accessToken, messageId).Pdfs.FirstOrDefault(p => p.PartId == partId);
            return pdf.Content == null ? throw new GmailMessageNotFoundException() : Task.FromResult(pdf.Content);
        }

        private List<FakeMessage> Mailbox(string accessToken) =>
            _mailboxes.GetOrAdd(accessToken["access:".Length..], _ => new List<FakeMessage>());

        private FakeMessage Find(string accessToken, string messageId)
        {
            var mailbox = Mailbox(accessToken);
            lock (mailbox)
                return mailbox.FirstOrDefault(m => m.Id == messageId) ?? throw new GmailMessageNotFoundException();
        }
    }
}
