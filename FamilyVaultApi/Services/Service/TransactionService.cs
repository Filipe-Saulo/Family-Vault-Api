using FamilyVaultApi.Common;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _repository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITransactionTypeRepository _transactionTypeRepository;

        public TransactionService(ITransactionRepository repository, ICategoryRepository categoryRepository, ITransactionTypeRepository transactionTypeRepository)
        {
            _repository = repository;
            _categoryRepository = categoryRepository;
            _transactionTypeRepository = transactionTypeRepository;
        }

        public async Task<TransactionResponseDto> CreateAsync(CreateTransactionDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var isAdmin = userClaims.IsInRole("Administrator");

            if (!isAdmin)
            {
                dto.UserId = userClaims.FindFirst("uid")?.Value;
            }
            else if (string.IsNullOrEmpty(dto.UserId))
            {
                throw new BadRequestException("Informe o UserId da transação.");
            }
            else if (!await _repository.UserExistsAsync(dto.UserId))
            {
                throw new NotFoundException("Usuário", dto.UserId);
            }

            await EnsureCategoryAllowsTransactionTypeAsync(dto.CategoryId, dto.TransactionTypeId);
            await EnsureCategoryAvailableToUserAsync(dto.CategoryId, dto.UserId);

            return await _repository.AddAsync(dto);
        }

        public async Task<PagedResult<TransactionResponseDto>> GetAllAsync(TransactionQueryRequestDto query, ClaimsPrincipal userClaims)
        {
            query ??= new TransactionQueryRequestDto();

            if (query.PageNumber <= 0) query.PageNumber = 1;
            if (query.PageSize <= 0) query.PageSize = 20;
            else if (query.PageSize > 100) query.PageSize = 100;

            var isAdmin = userClaims.IsInRole("Administrator");
            var hasManagePermission = userClaims.HasClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageTransactions));

            if (!isAdmin && !hasManagePermission)
            {
                var callerId = userClaims.FindFirst("uid")?.Value;
                if (string.IsNullOrEmpty(callerId))
                    throw new SecurityException("Você não tem permissão para listar transações.");

                query.UserId = callerId;
            }

            return await _repository.GetAllAsync(query);
        }

        public async Task<TransactionResponseDto> UpdateAsync(int id, UpdateTransactionDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            await EnsureCanModifyTransactionAsync(id, userClaims);
            await EnsureCategoryAllowsTransactionTypeAsync(dto.CategoryId, dto.TransactionTypeId);

            var ownerUserId = await _repository.GetOwnerUserIdAsync(id);
            if (ownerUserId == null)
                throw new NotFoundException("Transaction", id);

            await EnsureCategoryAvailableToUserAsync(dto.CategoryId, ownerUserId);

            return await _repository.UpdateAsync(id, dto);
        }

        public async Task DeleteAsync(int id, ClaimsPrincipal userClaims)
        {
            await EnsureCanModifyTransactionAsync(id, userClaims);
            await _repository.DeleteAsync(id);
        }

        private async Task EnsureCanModifyTransactionAsync(int transactionId, ClaimsPrincipal userClaims)
        {
            var isAdmin = userClaims.IsInRole("Administrator");
            var hasManagePermission = userClaims.HasClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageTransactions));

            if (isAdmin || hasManagePermission)
                return;

            var ownerUserId = await _repository.GetOwnerUserIdAsync(transactionId);
            if (ownerUserId == null)
                throw new NotFoundException("Transaction", transactionId);

            var callerId = userClaims.FindFirst("uid")?.Value;
            if (callerId != ownerUserId)
                throw new SecurityException("Você não tem permissão para alterar esta transação.");
        }

        // A categoria tem de ser global ou do usuário dono da transação
        private async Task EnsureCategoryAvailableToUserAsync(int categoryId, string? ownerUserId)
        {
            if (string.IsNullOrEmpty(ownerUserId) || !await _categoryRepository.IsAvailableToUserAsync(categoryId, ownerUserId))
                throw new BadRequestException("Categoria não disponível para este usuário.");
        }

        private async Task EnsureCategoryAllowsTransactionTypeAsync(int categoryId, int transactionTypeId)
        {
            var purpose = await _categoryRepository.GetPurposeCodeAsync(categoryId);
            if (purpose == null)
                throw new NotFoundException("Category não encontrada", categoryId);

            var type = await _transactionTypeRepository.GetCodeAsync(transactionTypeId);
            if (type == null)
                throw new NotFoundException("TransactionType não encontrado", transactionTypeId);

            if (purpose.Value.ToString() != type.Value.ToString())
                throw new BadRequestException("O tipo de transação selecionado não é compatível com o propósito da categoria.");
        }
    }
}
