namespace FamilyVaultApi.Services.IService
{
    // Isola o Google (OAuth e Gmail). Os testes de integração usam um falso; os unitários do cliente real simulam o HTTP.
    // O log de quem chama nunca leva token, corpo de e-mail ou conteúdo de anexo.
    public interface IGmailApiClient
    {
        string BuildAuthorizationUrl(string state);

        // Troca o code do callback pelo refresh token e lê o e-mail da conta autorizada
        Task<GmailAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

        Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);

        // Access token novo a partir do refresh token. invalid_grant lança GmailAccessRevokedException.
        Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken cancellationToken);

        // Ids das mensagens da busca, das mais recentes para as mais antigas, até maxResults
        Task<List<string>> ListMessageIdsAsync(string accessToken, string query, int maxResults, CancellationToken cancellationToken);

        // Remetente, data e anexos PDF. Mensagem apagada lança GmailMessageNotFoundException.
        Task<GmailMessageInfo> GetMessageAsync(string accessToken, string messageId, CancellationToken cancellationToken);

        // Conteúdo do anexo pelo partId (o attachmentId muda a cada leitura da mensagem)
        Task<byte[]> GetAttachmentAsync(string accessToken, string messageId, string partId, CancellationToken cancellationToken);
    }

    public class GmailAuthorization
    {
        public string? RefreshToken { get; set; }
        public string GoogleEmail { get; set; } = string.Empty;
        // Na tela de consentimento a pessoa pode desmarcar o escopo do Gmail
        public bool HasGmailReadScope { get; set; }
    }

    public class GmailMessageInfo
    {
        public string MessageId { get; set; } = string.Empty;
        // Endereço do From, em minúsculas; nulo se não der para ler
        public string? Sender { get; set; }
        public DateTime ReceivedAt { get; set; }
        public List<GmailPdfAttachment> PdfAttachments { get; set; } = new();
    }

    public class GmailPdfAttachment
    {
        public string PartId { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public class GmailAccessRevokedException : Exception
    {
        public GmailAccessRevokedException() : base("O acesso ao Gmail foi revogado.") { }
    }

    public class GmailMessageNotFoundException : Exception
    {
        public GmailMessageNotFoundException() : base("A mensagem não existe mais no Gmail.") { }
    }
}
