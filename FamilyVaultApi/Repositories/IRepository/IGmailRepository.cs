using FamilyVaultApi.Data.Entities;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IGmailRepository
    {
        // State do OAuth (uso único)
        Task AddStateAsync(GmailOAuthState state, DateTime now);
        Task<GmailOAuthState?> ConsumeStateAsync(string stateHash, DateTime now);

        // Conexão
        Task<GmailConnection?> GetConnectionAsync(string userId);
        // Grava (ou refaz) a conexão Active e cria a verificação Initial, no mesmo SaveChanges
        Task<GmailScan> ConnectAsync(string userId, string googleEmail, string refreshTokenEncrypted, GmailScan initialScan, DateTime now);
        // Apaga a conexão e os documentos do Gmail ainda Pending; verificações Running viram Failed
        Task DeleteConnectionAsync(string userId);
        // invalid_grant: conexão Revoked e verificações Running viram Failed; os documentos esperam a reconexão
        Task MarkRevokedAsync(string userId);

        // Verificações
        Task<bool> HasRunningScanAsync(string userId);
        // Trava de intervalo mínimo por UPDATE condicional; false = pedida há menos do intervalo
        Task<bool> TryMarkScanRequestedAsync(string userId, DateTime requestedBefore, DateTime now);
        Task<GmailScan> AddScanAsync(GmailScan scan);
        Task<GmailScan?> GetScanAsync(int scanId);
        Task<GmailScan?> GetNextRunningScanAsync();
        Task CompleteScanAsync(GmailScan scan, DateTime now);
        Task FailScanAsync(int scanId);
        Task<bool> IsScanRunningAsync(int scanId);
        Task UpdateScanCountsAsync(int scanId);

        // Documentos encontrados pela verificação
        Task<bool> HasDocumentForMessageAsync(string userId, string externalIdPrefix);
        // Devolve quantos foram criados (os que já existem pelo id externo são pulados)
        Task<int> AddDocumentsAsync(List<InboundDocument> documents);

        // Remetentes ignorados
        Task<List<GmailIgnoredSender>> ListIgnoredSendersAsync(string userId);
        Task<GmailIgnoredSender?> GetIgnoredSenderAsync(int id);
        Task DeleteIgnoredSenderAsync(int id);
        Task AddIgnoredSenderAsync(string userId, string sender, DateTime now);
    }
}
