using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FamilyVaultApi.Services.Service
{
    public class ChargeService : IChargeService
    {
        private readonly IChargeRepository _repository;
        private readonly ITransactionService _transactionService;
        private readonly ChargeProcessingOptions _options;
        private readonly TimeProvider _timeProvider;

        public ChargeService(IChargeRepository repository, ITransactionService transactionService, IOptions<ChargeProcessingOptions> options, TimeProvider timeProvider)
        {
            _repository = repository;
            _transactionService = transactionService;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<ChargeUploadResponseDto> UploadAsync(byte[] content, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            if (content == null || content.Length == 0)
                throw new BadRequestException("Envie um arquivo.");

            if (content.Length > _options.MaxUploadBytes)
                throw new BadRequestException($"O arquivo excede o tamanho máximo de {_options.MaxUploadBytes / (1024 * 1024)} MB.");

            // O tipo vem dos bytes iniciais, não do header enviado pelo cliente
            var contentType = DetectContentType(content)
                ?? throw new BadRequestException("Tipo de arquivo não suportado. Envie um PDF, JPEG ou PNG.");

            var externalId = "upload:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

            var existing = await _repository.FindDocumentByExternalIdAsync(userId, externalId);
            if (existing != null)
            {
                // Mesmo arquivo de um documento que falhou: tenta de novo em vez de devolver a falha
                if (existing.Status == InboundDocumentStatus.Failed)
                    await _repository.TryRequeueFailedDocumentAsync(existing.InboundDocumentId, content, contentType);

                return new ChargeUploadResponseDto { DocumentId = existing.InboundDocumentId };
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            if (await _repository.CountDocumentsCreatedSinceAsync(userId, dayStart) >= _options.DailyLimits.PerUser)
                throw new TooManyRequestsException("Limite diário de documentos atingido. Tente novamente amanhã.");

            var document = await _repository.AddDocumentAsync(new InboundDocument
            {
                UserId = userId,
                Source = InboundDocumentSource.Upload,
                ExternalId = externalId,
                ReceivedAt = now,
                Status = InboundDocumentStatus.Pending,
                TemporaryContent = content,
                ContentType = contentType,
                CreatedAt = now
            });

            return new ChargeUploadResponseDto { DocumentId = document.InboundDocumentId };
        }

        public async Task<PagedResult<ChargeResponseDto>> GetAllAsync(ChargeQueryRequestDto query, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            query ??= new ChargeQueryRequestDto();
            NormalizePaging(query);

            return await _repository.GetAllAsync(userId, query);
        }

        public async Task<ChargeResponseDto> GetByIdAsync(int id, ClaimsPrincipal userClaims)
        {
            await EnsureOwnerAsync(id, userClaims);

            return await _repository.GetResponseByIdAsync(id)
                ?? throw new NotFoundException("Conta", id);
        }

        public async Task<PagedResult<ChargeFailureResponseDto>> GetFailuresAsync(PagedFilterDto query, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            query ??= new PagedFilterDto();
            NormalizePaging(query);

            return await _repository.GetFailuresAsync(userId, query);
        }

        public async Task<TransactionResponseDto> ConfirmAsync(int id, ConfirmChargeDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var ownerUserId = await EnsureOwnerAsync(id, userClaims);
            await EnsurePendingReviewAsync(id);

            TransactionResponseDto? transaction = null;

            // Despesa e charge Confirmed na mesma transação de banco; se outro confirmou antes, desfaz tudo
            await _repository.ExecuteInTransactionAsync(async () =>
            {
                transaction = await _transactionService.CreateAsync(new CreateTransactionDto
                {
                    UserId = ownerUserId,
                    CategoryId = dto.CategoryId,
                    TransactionTypeId = (int)TransactionTypeCode.Expense,
                    Description = dto.Description,
                    Amount = dto.Amount,
                    TransactionDate = dto.DueDate!.Value
                }, userClaims);

                if (!await _repository.TryConfirmAsync(id, transaction.TransactionId))
                    throw new ConflictException("Esta conta já foi confirmada ou descartada.");
            });

            return transaction!;
        }

        public async Task DismissAsync(int id, DismissChargeDto? dto, ClaimsPrincipal userClaims)
        {
            await EnsureOwnerAsync(id, userClaims);

            // dto.IgnoreSender é ignorado até a leitura de e-mail existir
            if (!await _repository.TryDismissAsync(id))
                throw new ConflictException("Esta conta já foi confirmada ou descartada.");
        }

        // Charge é pessoal: nem o Administrator mexe na de outro usuário
        private async Task<string> EnsureOwnerAsync(int chargeId, ClaimsPrincipal userClaims)
        {
            var callerId = GetCallerId(userClaims);

            var ownerUserId = await _repository.GetOwnerUserIdAsync(chargeId);
            if (ownerUserId == null)
                throw new NotFoundException("Conta", chargeId);

            if (ownerUserId != callerId)
                throw new SecurityException("Você não tem permissão para acessar esta conta.");

            return ownerUserId;
        }

        private async Task EnsurePendingReviewAsync(int chargeId)
        {
            var charge = await _repository.GetResponseByIdAsync(chargeId)
                ?? throw new NotFoundException("Conta", chargeId);

            if (charge.Status != nameof(ChargeStatus.PendingReview))
                throw new ConflictException("Esta conta já foi confirmada ou descartada.");
        }

        private static string GetCallerId(ClaimsPrincipal userClaims)
        {
            var callerId = userClaims.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(callerId))
                throw new SecurityException("Você não tem permissão para acessar contas.");

            return callerId;
        }

        private static void NormalizePaging(PagedFilterDto query)
        {
            if (query.PageNumber <= 0) query.PageNumber = 1;
            if (query.PageSize <= 0) query.PageSize = 20;
            else if (query.PageSize > 100) query.PageSize = 100;
        }

        // %PDF, FF D8 FF (JPEG) e 89 50 4E 47 (PNG)
        private static string? DetectContentType(byte[] content)
        {
            if (content.AsSpan().StartsWith("%PDF"u8))
                return "application/pdf";
            if (content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
                return "image/jpeg";
            if (content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
                return "image/png";

            return null;
        }
    }
}
