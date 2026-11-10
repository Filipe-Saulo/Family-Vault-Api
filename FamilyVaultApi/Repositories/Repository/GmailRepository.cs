using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class GmailRepository : IGmailRepository
    {
        private readonly DatabaseContext _context;

        public GmailRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task AddStateAsync(GmailOAuthState state, DateTime now)
        {
            // Limpa os vencidos aqui mesmo: a tabela só cresce quando alguém conecta
            await _context.GmailOAuthStates.Where(s => s.ExpiresAt < now).ExecuteDeleteAsync();

            _context.GmailOAuthStates.Add(state);
            await _context.SaveChangesAsync();
        }

        public async Task<GmailOAuthState?> ConsumeStateAsync(string stateHash, DateTime now)
        {
            var state = await _context.GmailOAuthStates
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StateHash == stateHash);

            if (state == null)
                return null;

            // O DELETE decide quem usou primeiro: dois callbacks com o mesmo state, só um passa
            var deleted = await _context.GmailOAuthStates
                .Where(s => s.GmailOAuthStateId == state.GmailOAuthStateId)
                .ExecuteDeleteAsync();

            return deleted > 0 && state.ExpiresAt >= now ? state : null;
        }

        public async Task<GmailConnection?> GetConnectionAsync(string userId)
        {
            return await _context.GmailConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task<GmailScan> ConnectAsync(string userId, string googleEmail, string refreshTokenEncrypted, GmailScan initialScan, DateTime now)
        {
            await FailRunningScansAsync(userId);

            var connection = await _context.GmailConnections.FirstOrDefaultAsync(c => c.UserId == userId);

            // Outra conta do Google: os Pending da conta antiga não seriam encontrados na nova (igual ao DELETE)
            if (connection != null && !string.Equals(connection.GoogleEmail, googleEmail, StringComparison.OrdinalIgnoreCase))
                await DeletePendingGmailDocumentsAsync(userId);

            if (connection == null)
            {
                connection = new GmailConnection { UserId = userId };
                _context.GmailConnections.Add(connection);
            }

            // Reconexão (outra conta ou depois de Revoked) começa do zero: nova verificação inicial.
            // O id externo dos documentos evita gastar IA de novo com o que já foi lido.
            connection.GoogleEmail = googleEmail;
            connection.RefreshTokenEncrypted = refreshTokenEncrypted;
            connection.Status = GmailConnectionStatus.Active;
            connection.ConnectedAt = now;
            connection.InitialScanCompletedAt = null;
            connection.SyncedUntil = null;
            connection.LastScanRequestedAt = now;

            _context.GmailScans.Add(initialScan);
            await _context.SaveChangesAsync();

            return initialScan;
        }

        public async Task DeleteConnectionAsync(string userId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                await FailRunningScansAsync(userId);
                await DeletePendingGmailDocumentsAsync(userId);

                await _context.GmailConnections.Where(c => c.UserId == userId).ExecuteDeleteAsync();

                await transaction.CommitAsync();
            });
        }

        public async Task MarkRevokedAsync(string userId)
        {
            await _context.GmailConnections
                .Where(c => c.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, GmailConnectionStatus.Revoked));

            await FailRunningScansAsync(userId);
        }

        public async Task<bool> HasRunningScanAsync(string userId)
        {
            return await _context.GmailScans.AnyAsync(s => s.UserId == userId && s.Status == GmailScanStatus.Running);
        }

        public async Task<bool> TryMarkScanRequestedAsync(string userId, DateTime requestedBefore, DateTime now)
        {
            var rows = await _context.GmailConnections
                .Where(c => c.UserId == userId
                    && c.Status == GmailConnectionStatus.Active
                    && (c.LastScanRequestedAt == null || c.LastScanRequestedAt <= requestedBefore))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastScanRequestedAt, now));

            return rows > 0;
        }

        public async Task<GmailScan> AddScanAsync(GmailScan scan)
        {
            _context.GmailScans.Add(scan);
            await _context.SaveChangesAsync();
            return scan;
        }

        public async Task<GmailScan?> GetScanAsync(int scanId)
        {
            return await _context.GmailScans
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.GmailScanId == scanId);
        }

        public async Task<GmailScan?> GetNextRunningScanAsync()
        {
            return await _context.GmailScans
                .AsNoTracking()
                .Where(s => s.Status == GmailScanStatus.Running)
                .OrderBy(s => s.CreatedAt)
                .ThenBy(s => s.GmailScanId)
                .FirstOrDefaultAsync();
        }

        // Fecha a verificação e avança o ponto salvo para o início dela (WindowEnd), nunca para trás
        public async Task CompleteScanAsync(GmailScan scan, DateTime now)
        {
            var rows = await _context.GmailScans
                .Where(s => s.GmailScanId == scan.GmailScanId && s.Status == GmailScanStatus.Running)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, GmailScanStatus.Completed)
                    .SetProperty(x => x.CompletedAt, now));

            await UpdateScanCountsAsync(scan.GmailScanId);

            // Desconectada ou revogada no meio: a verificação já tinha virado Failed e o ponto não avança
            if (rows == 0)
                return;

            await _context.GmailConnections
                .Where(c => c.UserId == scan.UserId
                    && c.Status == GmailConnectionStatus.Active
                    && (c.SyncedUntil == null || c.SyncedUntil < scan.WindowEnd))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.SyncedUntil, scan.WindowEnd));

            if (scan.Kind == GmailScanKind.Initial)
            {
                await _context.GmailConnections
                    .Where(c => c.UserId == scan.UserId && c.Status == GmailConnectionStatus.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.InitialScanCompletedAt, now));
            }
        }

        public async Task FailScanAsync(int scanId)
        {
            await _context.GmailScans
                .Where(s => s.GmailScanId == scanId && s.Status == GmailScanStatus.Running)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, GmailScanStatus.Failed));

            await UpdateScanCountsAsync(scanId);
        }

        public async Task<bool> IsScanRunningAsync(int scanId)
        {
            return await _context.GmailScans.AnyAsync(s => s.GmailScanId == scanId && s.Status == GmailScanStatus.Running);
        }

        // found = documentos criados pela verificação; processed = os que terminaram (Done ou Failed, inclusive o PDF
        // grande, que já nasce Failed). Recontados durante a listagem, para o andamento nunca passar de 100%.
        public async Task UpdateScanCountsAsync(int scanId)
        {
            await _context.GmailScans
                .Where(s => s.GmailScanId == scanId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Found, x => _context.InboundDocuments.Count(d => d.GmailScanId == x.GmailScanId))
                    .SetProperty(x => x.Processed, x => _context.InboundDocuments.Count(d =>
                        d.GmailScanId == x.GmailScanId
                        && (d.Status == InboundDocumentStatus.Done || d.Status == InboundDocumentStatus.Failed))));
        }

        public async Task<bool> HasDocumentForMessageAsync(string userId, string externalIdPrefix)
        {
            return await _context.InboundDocuments
                .AnyAsync(d => d.UserId == userId && d.ExternalId.StartsWith(externalIdPrefix));
        }

        public async Task<int> AddDocumentsAsync(List<InboundDocument> documents)
        {
            var created = 0;

            foreach (var document in documents)
            {
                var exists = await _context.InboundDocuments
                    .AnyAsync(d => d.UserId == document.UserId && d.ExternalId == document.ExternalId);
                if (exists)
                    continue;

                _context.InboundDocuments.Add(document);
                try
                {
                    await _context.SaveChangesAsync();
                    created++;
                }
                catch (DbUpdateException)
                {
                    // Gravado ao mesmo tempo por outra verificação: fica o que venceu
                    _context.Entry(document).State = EntityState.Detached;
                }
            }

            return created;
        }

        public async Task<List<GmailIgnoredSender>> ListIgnoredSendersAsync(string userId)
        {
            return await _context.GmailIgnoredSenders
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.Sender)
                .ToListAsync();
        }

        public async Task<GmailIgnoredSender?> GetIgnoredSenderAsync(int id)
        {
            return await _context.GmailIgnoredSenders
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.GmailIgnoredSenderId == id);
        }

        public async Task DeleteIgnoredSenderAsync(int id)
        {
            await _context.GmailIgnoredSenders.Where(s => s.GmailIgnoredSenderId == id).ExecuteDeleteAsync();
        }

        public async Task AddIgnoredSenderAsync(string userId, string sender, DateTime now)
        {
            if (await _context.GmailIgnoredSenders.AnyAsync(s => s.UserId == userId && s.Sender == sender))
                return;

            var entity = new GmailIgnoredSender { UserId = userId, Sender = sender, CreatedAt = now };
            _context.GmailIgnoredSenders.Add(entity);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Dois descartes do mesmo remetente ao mesmo tempo: o índice único já garante um só
                _context.Entry(entity).State = EntityState.Detached;
            }
        }

        private async Task DeletePendingGmailDocumentsAsync(string userId)
        {
            await _context.InboundDocuments
                .Where(d => d.UserId == userId
                    && d.Source == InboundDocumentSource.Gmail
                    && d.Status == InboundDocumentStatus.Pending)
                .ExecuteDeleteAsync();
        }

        private async Task FailRunningScansAsync(string userId)
        {
            await _context.GmailScans
                .Where(s => s.UserId == userId && s.Status == GmailScanStatus.Running)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, GmailScanStatus.Failed));
        }
    }
}
