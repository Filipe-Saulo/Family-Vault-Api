using AutoMapper;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class ChargeRepository : IChargeRepository
    {
        // Janela, em dias, para considerar uma despesa parecida com o vencimento da conta
        private const int DuplicateWindowDays = 3;

        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;

        public ChargeRepository(DatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<InboundDocument?> FindDocumentByExternalIdAsync(string userId, string externalId)
        {
            return await _context.InboundDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userId && d.ExternalId == externalId);
        }

        public async Task<int> CountDocumentsCreatedSinceAsync(string userId, DateTime since)
        {
            return await _context.InboundDocuments
                .CountAsync(d => d.UserId == userId && d.CreatedAt >= since);
        }

        public async Task<InboundDocument> AddDocumentAsync(InboundDocument document)
        {
            _context.InboundDocuments.Add(document);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Outro pedido gravou o mesmo arquivo ao mesmo tempo: devolve o documento que venceu
                _context.Entry(document).State = EntityState.Detached;

                var existing = await FindDocumentByExternalIdAsync(document.UserId, document.ExternalId);
                if (existing == null)
                    throw;

                return existing;
            }

            return document;
        }

        // Reenvio de um documento que falhou: volta para a fila com as tentativas zeradas e o conteúdo de novo
        public async Task<bool> TryRequeueFailedDocumentAsync(int documentId, byte[] content, string contentType)
        {
            var rows = await _context.InboundDocuments
                .Where(d => d.InboundDocumentId == documentId && d.Status == InboundDocumentStatus.Failed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, InboundDocumentStatus.Pending)
                    .SetProperty(d => d.Attempts, 0)
                    .SetProperty(d => d.FailureReason, (string?)null)
                    .SetProperty(d => d.ProcessingStartedAt, (DateTime?)null)
                    .SetProperty(d => d.TemporaryContent, content)
                    .SetProperty(d => d.ContentType, contentType));

            return rows > 0;
        }

        public async Task<(InboundDocumentSource Source, string? Sender)?> GetDocumentSenderAsync(int chargeId)
        {
            var document = await _context.Charges
                .Where(c => c.ChargeId == chargeId)
                .Select(c => new { c.InboundDocument.Source, c.InboundDocument.Sender })
                .FirstOrDefaultAsync();

            return document == null ? null : (document.Source, document.Sender);
        }

        public async Task<string?> GetOwnerUserIdAsync(int chargeId)
        {
            return await _context.Charges
                .Where(c => c.ChargeId == chargeId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
        }

        public async Task<ChargeResponseDto?> GetResponseByIdAsync(int chargeId)
        {
            var entity = await _context.Charges
                .AsNoTracking()
                .Include(c => c.InboundDocument)
                .FirstOrDefaultAsync(c => c.ChargeId == chargeId);

            return entity == null ? null : _mapper.Map<ChargeResponseDto>(entity);
        }

        public async Task<PagedResult<ChargeResponseDto>> GetAllAsync(string userId, ChargeQueryRequestDto query)
        {
            var q = _context.Charges.AsNoTracking().Where(c => c.UserId == userId);

            if (query.Status.HasValue)
                q = q.Where(c => c.Status == query.Status.Value);

            var total = await q.CountAsync();

            var items = await q
                .Include(c => c.InboundDocument)
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.ChargeId)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new PagedResult<ChargeResponseDto>
            {
                TotalCount = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = _mapper.Map<List<ChargeResponseDto>>(items)
            };
        }

        public async Task<PagedResult<ChargeFailureResponseDto>> GetFailuresAsync(string userId, PagedFilterDto query)
        {
            var q = _context.InboundDocuments
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.Status == InboundDocumentStatus.Failed);

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(d => d.CreatedAt)
                .ThenByDescending(d => d.InboundDocumentId)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new PagedResult<ChargeFailureResponseDto>
            {
                TotalCount = total,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = _mapper.Map<List<ChargeFailureResponseDto>>(items)
            };
        }

        // EnableRetryOnFailure exige que a transação manual rode dentro da execution strategy
        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                await action();
                await transaction.CommitAsync();
            });
        }

        public async Task<bool> TryConfirmAsync(int chargeId, int transactionId)
        {
            var rows = await _context.Charges
                .Where(c => c.ChargeId == chargeId && c.Status == ChargeStatus.PendingReview)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, ChargeStatus.Confirmed)
                    .SetProperty(c => c.TransactionId, transactionId));

            return rows > 0;
        }

        public async Task<bool> TryDismissAsync(int chargeId)
        {
            var rows = await _context.Charges
                .Where(c => c.ChargeId == chargeId && c.Status == ChargeStatus.PendingReview)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ChargeStatus.Dismissed));

            return rows > 0;
        }

        public async Task<int> CountDocumentsStartedSinceAsync(DateTime since)
        {
            return await _context.InboundDocuments.CountAsync(d => d.ProcessingStartedAt >= since);
        }

        // O Pending mais antigo de quem ainda não chegou ao limite do dia; os demais esperam o dia seguinte
        public async Task<List<int>> GetPendingCandidateIdsAsync(DateTime dayStart, DateTime retryReadyBefore, int perUserLimit, int take)
        {
            var startedToday = await _context.InboundDocuments
                .Where(d => d.ProcessingStartedAt >= dayStart)
                .GroupBy(d => d.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToListAsync();

            var blockedUsers = startedToday
                .Where(x => x.Count >= perUserLimit)
                .Select(x => x.UserId)
                .ToList();

            return await _context.InboundDocuments
                .Where(d => d.Status == InboundDocumentStatus.Pending && !blockedUsers.Contains(d.UserId))
                // Documento do Gmail só anda com a conexão ativa; desconectado ou revogado, espera a reconexão
                .Where(d => d.Source == InboundDocumentSource.Upload
                    || _context.GmailConnections.Any(c => c.UserId == d.UserId && c.Status == GmailConnectionStatus.Active))
                // Quem voltou da tentativa anterior espera o intervalo antes da próxima
                .Where(d => d.ProcessingStartedAt == null || d.ProcessingStartedAt <= retryReadyBefore)
                .OrderBy(d => d.CreatedAt)
                .ThenBy(d => d.InboundDocumentId)
                .Select(d => d.InboundDocumentId)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> TryReserveDocumentAsync(int documentId, DateTime now)
        {
            var rows = await _context.InboundDocuments
                .Where(d => d.InboundDocumentId == documentId && d.Status == InboundDocumentStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, InboundDocumentStatus.Processing)
                    .SetProperty(d => d.Attempts, d => d.Attempts + 1)
                    .SetProperty(d => d.ProcessingStartedAt, now));

            return rows > 0;
        }

        public async Task<InboundDocument?> GetDocumentAsync(int documentId)
        {
            return await _context.InboundDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.InboundDocumentId == documentId);
        }

        public async Task ReturnDocumentToPendingAsync(int documentId)
        {
            await _context.InboundDocuments
                .Where(d => d.InboundDocumentId == documentId && d.Status == InboundDocumentStatus.Processing)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, InboundDocumentStatus.Pending));
        }

        // Devolve à fila sem gastar a tentativa nem contar no limite do dia (o Gmail ficou indisponível para a pessoa)
        public async Task ReleaseDocumentAsync(int documentId)
        {
            await _context.InboundDocuments
                .Where(d => d.InboundDocumentId == documentId && d.Status == InboundDocumentStatus.Processing)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, InboundDocumentStatus.Pending)
                    .SetProperty(d => d.Attempts, d => d.Attempts > 0 ? d.Attempts - 1 : 0)
                    .SetProperty(d => d.ProcessingStartedAt, (DateTime?)null));
        }

        // Termina o documento, apaga o conteúdo e grava a charge (se houver) no mesmo SaveChanges
        public async Task CompleteDocumentAsync(int documentId, InboundDocumentStatus status, string? failureReason, Charge? charge)
        {
            var document = await _context.InboundDocuments.FirstAsync(d => d.InboundDocumentId == documentId);

            document.Status = status;
            document.FailureReason = failureReason;
            document.TemporaryContent = null;

            if (charge != null)
            {
                charge.InboundDocumentId = documentId;
                _context.Charges.Add(charge);
            }

            await _context.SaveChangesAsync();

            if (document.GmailScanId.HasValue)
                await UpdateScanProgressAsync(new List<int> { document.GmailScanId.Value });
        }

        // processed da verificação = documentos dela que terminaram (Done ou Failed); recontado em vez de somado,
        // para um documento terminado por caminhos diferentes não contar duas vezes
        private async Task UpdateScanProgressAsync(List<int> scanIds)
        {
            await _context.GmailScans
                .Where(s => scanIds.Contains(s.GmailScanId))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Processed, x => _context.InboundDocuments.Count(d =>
                    d.GmailScanId == x.GmailScanId
                    && (d.Status == InboundDocumentStatus.Done || d.Status == InboundDocumentStatus.Failed))));
        }

        // O que ficou preso em Processing volta a Pending; sem tentativas sobrando, termina como Failed
        public async Task<int> RequeueStuckDocumentsAsync(DateTime startedBefore, int maxAttempts)
        {
            var failingScanIds = await _context.InboundDocuments
                .Where(d => d.Status == InboundDocumentStatus.Processing
                    && d.ProcessingStartedAt < startedBefore
                    && d.Attempts >= maxAttempts
                    && d.GmailScanId != null)
                .Select(d => d.GmailScanId!.Value)
                .Distinct()
                .ToListAsync();

            await _context.InboundDocuments
                .Where(d => d.Status == InboundDocumentStatus.Processing
                    && d.ProcessingStartedAt < startedBefore
                    && d.Attempts >= maxAttempts)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, InboundDocumentStatus.Failed)
                    .SetProperty(d => d.FailureReason, "Não foi possível processar o documento.")
                    .SetProperty(d => d.TemporaryContent, (byte[]?)null));

            if (failingScanIds.Count > 0)
                await UpdateScanProgressAsync(failingScanIds);

            return await _context.InboundDocuments
                .Where(d => d.Status == InboundDocumentStatus.Processing && d.ProcessingStartedAt < startedBefore)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, InboundDocumentStatus.Pending));
        }

        public async Task<List<ChargeCategoryOption>> GetExpenseCategoriesAsync(string userId)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Purpose.Code == "expense" && (c.UserId == null || c.UserId == userId))
                .OrderBy(c => c.Description)
                .Select(c => new { c.CategoryId, c.Description })
                .ToListAsync();

            return categories.Select(c => new ChargeCategoryOption(c.CategoryId, c.Description)).ToList();
        }

        public async Task<bool> UserHasChargeWithBarcodeAsync(string userId, string barcode)
        {
            return await _context.Charges.AnyAsync(c => c.UserId == userId && c.Barcode == barcode);
        }

        // Despesa do usuário com o mesmo valor e data a até 3 dias do vencimento; a de data mais próxima
        public async Task<int?> FindPossibleDuplicateTransactionIdAsync(string userId, decimal amount, DateTime dueDate)
        {
            var from = dueDate.Date.AddDays(-DuplicateWindowDays);
            var to = dueDate.Date.AddDays(DuplicateWindowDays + 1);

            var candidates = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                    && t.TransactionType.Code == "expense"
                    && t.Amount == amount
                    && t.TransactionDate >= from
                    && t.TransactionDate < to)
                .Select(t => new { t.TransactionId, t.TransactionDate })
                .ToListAsync();

            return candidates
                .OrderBy(t => Math.Abs((t.TransactionDate.Date - dueDate.Date).TotalDays))
                .ThenBy(t => t.TransactionId)
                .Select(t => (int?)t.TransactionId)
                .FirstOrDefault();
        }
    }
}
