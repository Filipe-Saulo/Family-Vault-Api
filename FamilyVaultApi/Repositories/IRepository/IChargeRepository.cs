using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IChargeRepository
    {
        // Upload
        Task<InboundDocument?> FindDocumentByExternalIdAsync(string userId, string externalId);
        Task<int> CountDocumentsCreatedSinceAsync(string userId, DateTime since);
        Task<InboundDocument> AddDocumentAsync(InboundDocument document);
        Task<bool> TryRequeueFailedDocumentAsync(int documentId, byte[] content, string contentType);

        // Consulta
        Task<string?> GetOwnerUserIdAsync(int chargeId);
        Task<(InboundDocumentSource Source, string? Sender)?> GetDocumentSenderAsync(int chargeId);
        Task<ChargeResponseDto?> GetResponseByIdAsync(int chargeId);
        Task<PagedResult<ChargeResponseDto>> GetAllAsync(string userId, ChargeQueryRequestDto query);
        Task<PagedResult<ChargeFailureResponseDto>> GetFailuresAsync(string userId, PagedFilterDto query, int maxPasswordAttempts);

        // Confirmação e descarte (UPDATE condicional em status = PendingReview; false = outro já decidiu)
        Task ExecuteInTransactionAsync(Func<Task> action);
        Task<bool> TryConfirmAsync(int chargeId, int transactionId);
        Task<bool> TryDismissAsync(int chargeId);

        // Worker
        Task<int> CountDocumentsStartedSinceAsync(DateTime since);
        Task<List<int>> GetPendingCandidateIdsAsync(DateTime dayStart, DateTime retryReadyBefore, int perUserLimit, int take);
        Task<bool> TryReserveDocumentAsync(int documentId, DateTime now);
        Task<InboundDocument?> GetDocumentAsync(int documentId);
        Task ReturnDocumentToPendingAsync(int documentId);
        Task ReleaseDocumentAsync(int documentId);
        Task CompleteDocumentAsync(int documentId, InboundDocumentStatus status, string? failureReason, Charge? charge);
        Task<int> RequeueStuckDocumentsAsync(DateTime startedBefore, int maxAttempts);
        Task<List<ChargeCategoryOption>> GetExpenseCategoriesAsync(string userId);
        Task<bool> UserHasChargeWithBarcodeAsync(string userId, string barcode);
        Task<int?> FindPossibleDuplicateTransactionIdAsync(string userId, decimal amount, DateTime dueDate);

        // PDF com senha
        Task MarkPasswordRequiredAsync(int documentId, string reason, DateTime now, bool keepContent);
        Task<bool> TryReservePasswordAttemptAsync(int documentId, int maxAttempts);
        Task<bool> TryRequeueUnlockedDocumentAsync(int documentId, byte[] unlockedContent);
        Task<bool> TryCompleteManuallyAsync(int documentId, Charge charge);
        Task<int> ExpirePasswordRequiredUploadsAsync(DateTime requiredBefore, string reason);
        Task<string?> GetSenderPasswordAsync(string userId, string sender);
        Task SaveSenderPasswordAsync(string userId, string sender, string passwordEncrypted, DateTime now);
    }
}
