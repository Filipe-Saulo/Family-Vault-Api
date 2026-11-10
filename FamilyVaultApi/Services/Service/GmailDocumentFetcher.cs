using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using System.Security.Cryptography;

namespace FamilyVaultApi.Services.Service
{
    // Etapa 2 do worker para documento do Gmail: baixa o PDF pelo id da mensagem e o partId. O PDF não é guardado.
    public class GmailDocumentFetcher
    {
        public const string ExternalIdPrefix = "gmail:";

        private readonly IGmailRepository _repository;
        private readonly IGmailApiClient _client;
        private readonly GmailTokenProtector _protector;

        public GmailDocumentFetcher(IGmailRepository repository, IGmailApiClient client, GmailTokenProtector protector)
        {
            _repository = repository;
            _client = client;
            _protector = protector;
        }

        public static string BuildExternalId(string messageId, string partId) => $"{ExternalIdPrefix}{messageId}:{partId}";

        public static string MessagePrefix(string messageId) => $"{ExternalIdPrefix}{messageId}:";

        // Sem conexão ativa ou com o acesso retirado lança GmailAccessRevokedException (o documento espera a reconexão);
        // e-mail apagado lança GmailMessageNotFoundException.
        public async Task<byte[]> FetchAsync(InboundDocument document, CancellationToken cancellationToken)
        {
            var (messageId, partId) = ParseExternalId(document.ExternalId);

            var connection = await _repository.GetConnectionAsync(document.UserId);
            if (connection == null || connection.Status != GmailConnectionStatus.Active)
                throw new GmailAccessRevokedException();

            string accessToken;
            try
            {
                accessToken = await _client.GetAccessTokenAsync(_protector.Unprotect(connection.RefreshTokenEncrypted), cancellationToken);
            }
            catch (Exception ex) when (ex is GmailAccessRevokedException or CryptographicException or FormatException)
            {
                // Chave de criptografia trocada também deixa o token inútil: a pessoa reconecta
                await _repository.MarkRevokedAsync(document.UserId);
                throw new GmailAccessRevokedException();
            }

            try
            {
                return await _client.GetAttachmentAsync(accessToken, messageId, partId, cancellationToken);
            }
            catch (GmailAccessRevokedException)
            {
                await _repository.MarkRevokedAsync(document.UserId);
                throw;
            }
        }

        private static (string MessageId, string PartId) ParseExternalId(string externalId)
        {
            var parts = externalId.Split(':');
            if (parts.Length != 3 || parts[0] + ":" != ExternalIdPrefix)
                throw new GmailMessageNotFoundException();

            return (parts[1], parts[2]);
        }
    }
}
