namespace FamilyVaultApi.Models.Internal
{
    // Seção "Gmail" do appsettings. ClientId, ClientSecret e TokenEncryptionKey ficam em user-secrets ou nas variáveis
    // Gmail__ClientId, Gmail__ClientSecret e Gmail__TokenEncryptionKey, nunca em arquivo do repositório. Sem as três,
    // a integração fica desligada: as rotas de conexão e de verificação respondem 400.
    public class GmailOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        // 32 bytes em base64 (AES-256-GCM do refresh token)
        public string? TokenEncryptionKey { get; set; }
        // URI do GET /api/gmail/callback, igual à cadastrada no client do Google Cloud
        public string? RedirectUri { get; set; }
        // Para onde o callback pode voltar (app e painel). Sem returnUrl no connect, usa o primeiro.
        public List<string> AllowedReturnUrls { get; set; } = new();

        public int StateLifetimeMinutes { get; set; } = 10;
        public int InitialScanDays { get; set; } = 90;
        public int InitialScanMaxDocuments { get; set; } = 50;
        public int ManualScanMaxDays { get; set; } = 30;
        public int ManualScanMinIntervalSeconds { get; set; } = 60;
        // A busca começa este tanto antes do ponto salvo, para cobrir e-mail entregue com atraso
        public int LookbackMinutes { get; set; } = 60;
        // Teto de e-mails lidos por verificação (proteção, não regra: o custo de IA já tem o limite diário)
        public int MaxMessagesPerScan { get; set; } = 500;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret)
            && !string.IsNullOrWhiteSpace(TokenEncryptionKey);
    }
}
