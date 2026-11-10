using System.Security;
using System.Text;
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
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    // Relógio controlável para os testes que dependem do corte do dia UTC
    public class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public FakeTimeProvider(DateTimeOffset now) => Now = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    public class ChargeServiceTests
    {
        private const string UserId = "user-1";

        private readonly Mock<IChargeRepository> _repository = new();
        private readonly Mock<IGmailRepository> _gmailRepository = new();
        private readonly Mock<ITransactionService> _transactionService = new();
        private readonly ChargeProcessingOptions _options = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 23, 59, 0, TimeSpan.Zero));
        private readonly ChargeService _service;

        public ChargeServiceTests()
        {
            _options.DailyLimits.PerUser = 3;
            _service = new ChargeService(_repository.Object, _gmailRepository.Object, _transactionService.Object, Options.Create(_options), _time);

            _repository.Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns((Func<Task> action) => action());
        }

        private static byte[] Pdf(string extra = "x") => Encoding.ASCII.GetBytes("%PDF-1.4 " + extra);

        // ---------- Upload ----------

        [Fact]
        public async Task UploadAsync_ValidPdf_ShouldStoreDocumentWithHashTypeAndUtcDate()
        {
            InboundDocument? saved = null;
            _repository.Setup(r => r.AddDocumentAsync(It.IsAny<InboundDocument>()))
                .ReturnsAsync((InboundDocument d) => { saved = d; d.InboundDocumentId = 77; return d; });
            var content = Pdf();

            var result = await _service.UploadAsync(content, ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.DocumentId.Should().Be(77);
            saved!.UserId.Should().Be(UserId);
            saved.Source.Should().Be(InboundDocumentSource.Upload);
            saved.Status.Should().Be(InboundDocumentStatus.Pending);
            saved.ContentType.Should().Be("application/pdf");
            saved.ExternalId.Should().Be("upload:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant());
            saved.ReceivedAt.Should().Be(_time.Now.UtcDateTime);
            saved.TemporaryContent.Should().BeEquivalentTo(content);
        }

        [Theory]
        [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }, "image/jpeg")]
        [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00 }, "image/png")]
        public async Task UploadAsync_JpegOrPng_ShouldDetectTypeFromBytes(byte[] content, string expected)
        {
            InboundDocument? saved = null;
            _repository.Setup(r => r.AddDocumentAsync(It.IsAny<InboundDocument>()))
                .ReturnsAsync((InboundDocument d) => { saved = d; return d; });

            await _service.UploadAsync(content, ClaimsPrincipalTestHelper.CreateUser(UserId));

            saved!.ContentType.Should().Be(expected);
        }

        [Fact]
        public async Task UploadAsync_SameFileAlreadySent_ShouldReturnExistingDocumentWithoutAdding()
        {
            _repository.Setup(r => r.FindDocumentByExternalIdAsync(UserId, It.IsAny<string>()))
                .ReturnsAsync(new InboundDocument { InboundDocumentId = 5 });

            var result = await _service.UploadAsync(Pdf(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.DocumentId.Should().Be(5);
            _repository.Verify(r => r.AddDocumentAsync(It.IsAny<InboundDocument>()), Times.Never);
            _repository.Verify(r => r.CountDocumentsCreatedSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_SameFileOfFailedDocument_ShouldRequeueExistingDocument()
        {
            var content = Pdf();
            _repository.Setup(r => r.FindDocumentByExternalIdAsync(UserId, It.IsAny<string>()))
                .ReturnsAsync(new InboundDocument { InboundDocumentId = 5, Status = InboundDocumentStatus.Failed });

            var result = await _service.UploadAsync(content, ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.DocumentId.Should().Be(5);
            _repository.Verify(r => r.TryRequeueFailedDocumentAsync(5, content, "application/pdf"), Times.Once);
            _repository.Verify(r => r.AddDocumentAsync(It.IsAny<InboundDocument>()), Times.Never);
        }

        [Theory]
        [InlineData(InboundDocumentStatus.Pending)]
        [InlineData(InboundDocumentStatus.Processing)]
        [InlineData(InboundDocumentStatus.Done)]
        public async Task UploadAsync_SameFileOfNotFailedDocument_ShouldNotRequeue(InboundDocumentStatus status)
        {
            _repository.Setup(r => r.FindDocumentByExternalIdAsync(UserId, It.IsAny<string>()))
                .ReturnsAsync(new InboundDocument { InboundDocumentId = 5, Status = status });

            await _service.UploadAsync(Pdf(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            _repository.Verify(r => r.TryRequeueFailedDocumentAsync(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_AtPerUserLimit_ShouldThrowTooManyRequestsCountingFromUtcDayStart()
        {
            _repository.Setup(r => r.CountDocumentsCreatedSinceAsync(UserId, It.IsAny<DateTime>())).ReturnsAsync(3);

            Func<Task> act = () => _service.UploadAsync(Pdf(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _repository.Verify(r => r.CountDocumentsCreatedSinceAsync(UserId, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)), Times.Once);
            _repository.Verify(r => r.AddDocumentAsync(It.IsAny<InboundDocument>()), Times.Never);
        }

        [Fact]
        public async Task UploadAsync_AfterUtcMidnight_ShouldCountFromTheNewDay()
        {
            _time.Now = new DateTimeOffset(2026, 11, 11, 0, 0, 1, TimeSpan.Zero);
            _repository.Setup(r => r.AddDocumentAsync(It.IsAny<InboundDocument>())).ReturnsAsync((InboundDocument d) => d);

            await _service.UploadAsync(Pdf(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            _repository.Verify(r => r.CountDocumentsCreatedSinceAsync(UserId, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc)), Times.Once);
        }

        [Fact]
        public async Task UploadAsync_BelowLimit_ShouldNotThrow()
        {
            _repository.Setup(r => r.CountDocumentsCreatedSinceAsync(UserId, It.IsAny<DateTime>())).ReturnsAsync(2);
            _repository.Setup(r => r.AddDocumentAsync(It.IsAny<InboundDocument>())).ReturnsAsync((InboundDocument d) => d);

            Func<Task> act = () => _service.UploadAsync(Pdf(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UploadAsync_EmptyContent_ShouldThrowBadRequest()
        {
            Func<Task> act = () => _service.UploadAsync([], ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task UploadAsync_AboveMaxBytes_ShouldThrowBadRequest()
        {
            _options.MaxUploadBytes = 10;
            var content = Encoding.ASCII.GetBytes("%PDF-1.4 0123456789");

            Func<Task> act = () => _service.UploadAsync(content, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task UploadAsync_UnknownType_ShouldThrowBadRequest()
        {
            Func<Task> act = () => _service.UploadAsync(Encoding.UTF8.GetBytes("texto simples"), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task UploadAsync_WithoutUidClaim_ShouldThrowSecurityException()
        {
            Func<Task> act = () => _service.UploadAsync(Pdf(), ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<SecurityException>();
        }

        // ---------- Consulta e paginação ----------

        [Theory]
        [InlineData(0, 0, 1, 20)]
        [InlineData(-3, -1, 1, 20)]
        [InlineData(2, 500, 2, 100)]
        [InlineData(4, 50, 4, 50)]
        public async Task GetAllAsync_PagingOutOfRange_ShouldNormalize(int pageNumber, int pageSize, int expectedPage, int expectedSize)
        {
            ChargeQueryRequestDto? received = null;
            _repository.Setup(r => r.GetAllAsync(UserId, It.IsAny<ChargeQueryRequestDto>()))
                .Callback((string _, ChargeQueryRequestDto q) => received = q)
                .ReturnsAsync(new PagedResult<ChargeResponseDto>());

            await _service.GetAllAsync(new ChargeQueryRequestDto { PageNumber = pageNumber, PageSize = pageSize, Status = ChargeStatus.Dismissed },
                ClaimsPrincipalTestHelper.CreateUser(UserId));

            received!.PageNumber.Should().Be(expectedPage);
            received.PageSize.Should().Be(expectedSize);
            received.Status.Should().Be(ChargeStatus.Dismissed);
        }

        [Fact]
        public async Task GetAllAsync_ByAdministrator_ShouldStillScopeToCallerOwnCharges()
        {
            _repository.Setup(r => r.GetAllAsync("admin-1", It.IsAny<ChargeQueryRequestDto>())).ReturnsAsync(new PagedResult<ChargeResponseDto>());

            await _service.GetAllAsync(new ChargeQueryRequestDto(), ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            _repository.Verify(r => r.GetAllAsync("admin-1", It.IsAny<ChargeQueryRequestDto>()), Times.Once);
        }

        [Fact]
        public async Task GetFailuresAsync_PagingOutOfRange_ShouldNormalize()
        {
            PagedFilterDto? received = null;
            _repository.Setup(r => r.GetFailuresAsync(UserId, It.IsAny<PagedFilterDto>()))
                .Callback((string _, PagedFilterDto q) => received = q)
                .ReturnsAsync(new PagedResult<ChargeFailureResponseDto>());

            await _service.GetFailuresAsync(new PagedFilterDto { PageNumber = 0, PageSize = 1000 }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            received!.PageNumber.Should().Be(1);
            received.PageSize.Should().Be(100);
        }

        [Fact]
        public async Task GetByIdAsync_Owner_ShouldReturnCharge()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.GetResponseByIdAsync(9)).ReturnsAsync(new ChargeResponseDto { ChargeId = 9 });

            var result = await _service.GetByIdAsync(9, ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.ChargeId.Should().Be(9);
        }

        [Fact]
        public async Task GetByIdAsync_UnknownCharge_ShouldThrowNotFound()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync((string?)null);

            Func<Task> act = () => _service.GetByIdAsync(9, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task GetByIdAsync_ChargeOfAnotherUser_ShouldThrowSecurityEvenForAdministrator()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync("other");

            Func<Task> act = () => _service.GetByIdAsync(9, ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            await act.Should().ThrowAsync<SecurityException>();
            _repository.Verify(r => r.GetResponseByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // ---------- Confirm ----------

        private static ConfirmChargeDto ConfirmDto() => new()
        {
            Description = "Conta de luz",
            Amount = 150.25m,
            DueDate = new DateTime(2026, 12, 5),
            CategoryId = 2
        };

        private void SetupPendingCharge(int id = 9)
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(id)).ReturnsAsync(UserId);
            _repository.Setup(r => r.GetResponseByIdAsync(id))
                .ReturnsAsync(new ChargeResponseDto { ChargeId = id, Status = nameof(ChargeStatus.PendingReview) });
        }

        [Fact]
        public async Task ConfirmAsync_PendingCharge_ShouldCreateExpenseOnDueDateAndMarkConfirmed()
        {
            SetupPendingCharge();
            CreateTransactionDto? created = null;
            _transactionService.Setup(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Callback((CreateTransactionDto dto, System.Security.Claims.ClaimsPrincipal _) => created = dto)
                .ReturnsAsync(new TransactionResponseDto { TransactionId = 55 });
            _repository.Setup(r => r.TryConfirmAsync(9, 55)).ReturnsAsync(true);

            var result = await _service.ConfirmAsync(9, ConfirmDto(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.TransactionId.Should().Be(55);
            created!.UserId.Should().Be(UserId);
            created.TransactionTypeId.Should().Be(1);
            created.TransactionDate.Should().Be(new DateTime(2026, 12, 5));
            created.Amount.Should().Be(150.25m);
            created.CategoryId.Should().Be(2);
            _repository.Verify(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmAsync_TryConfirmReturnsFalse_ShouldThrowConflict()
        {
            SetupPendingCharge();
            _transactionService.Setup(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(new TransactionResponseDto { TransactionId = 55 });
            _repository.Setup(r => r.TryConfirmAsync(9, 55)).ReturnsAsync(false);

            Func<Task> act = () => _service.ConfirmAsync(9, ConfirmDto(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
        }

        [Theory]
        [InlineData(nameof(ChargeStatus.Confirmed))]
        [InlineData(nameof(ChargeStatus.Dismissed))]
        public async Task ConfirmAsync_NotPendingReview_ShouldThrowConflictWithoutCreatingTransaction(string status)
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.GetResponseByIdAsync(9)).ReturnsAsync(new ChargeResponseDto { ChargeId = 9, Status = status });

            Func<Task> act = () => _service.ConfirmAsync(9, ConfirmDto(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
            _transactionService.Verify(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmAsync_ChargeOfAnotherUser_ShouldThrowSecurityWithoutCreatingTransaction()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync("other");

            Func<Task> act = () => _service.ConfirmAsync(9, ConfirmDto(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<SecurityException>();
            _transactionService.Verify(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmAsync_UnknownCharge_ShouldThrowNotFound()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync((string?)null);

            Func<Task> act = () => _service.ConfirmAsync(9, ConfirmDto(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task ConfirmAsync_NullDto_ShouldThrowArgumentNull()
        {
            Func<Task> act = () => _service.ConfirmAsync(9, null!, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        // ---------- Dismiss ----------

        [Fact]
        public async Task DismissAsync_Owner_ShouldCallTryDismiss()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.TryDismissAsync(9)).ReturnsAsync(true);

            await _service.DismissAsync(9, null, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _repository.Verify(r => r.TryDismissAsync(9), Times.Once);
        }

        [Fact]
        public async Task DismissAsync_GmailChargeWithIgnoreSender_ShouldIgnoreTheSender()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.TryDismissAsync(9)).ReturnsAsync(true);
            _repository.Setup(r => r.GetDocumentSenderAsync(9)).ReturnsAsync((InboundDocumentSource.Gmail, "cobranca@loja.test"));

            await _service.DismissAsync(9, new DismissChargeDto { IgnoreSender = true }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _gmailRepository.Verify(r => r.AddIgnoredSenderAsync(UserId, "cobranca@loja.test", _time.Now.UtcDateTime), Times.Once);
        }

        [Fact]
        public async Task DismissAsync_UploadChargeWithIgnoreSender_ShouldNotIgnoreAnything()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.TryDismissAsync(9)).ReturnsAsync(true);
            _repository.Setup(r => r.GetDocumentSenderAsync(9)).ReturnsAsync((InboundDocumentSource.Upload, (string?)null));

            await _service.DismissAsync(9, new DismissChargeDto { IgnoreSender = true }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _gmailRepository.Verify(r => r.AddIgnoredSenderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task DismissAsync_GmailChargeWithoutIgnoreSender_ShouldNotIgnoreTheSender()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.TryDismissAsync(9)).ReturnsAsync(true);
            _repository.Setup(r => r.GetDocumentSenderAsync(9)).ReturnsAsync((InboundDocumentSource.Gmail, "cobranca@loja.test"));

            await _service.DismissAsync(9, new DismissChargeDto { IgnoreSender = false }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _gmailRepository.Verify(r => r.AddIgnoredSenderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task DismissAsync_TryDismissReturnsFalse_ShouldThrowConflict()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync(UserId);
            _repository.Setup(r => r.TryDismissAsync(9)).ReturnsAsync(false);

            Func<Task> act = () => _service.DismissAsync(9, new DismissChargeDto { IgnoreSender = true }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
            _gmailRepository.Verify(r => r.AddIgnoredSenderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task DismissAsync_ChargeOfAnotherUser_ShouldThrowSecurityWithoutDismissing()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync("other");

            Func<Task> act = () => _service.DismissAsync(9, null, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<SecurityException>();
            _repository.Verify(r => r.TryDismissAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DismissAsync_UnknownCharge_ShouldThrowNotFound()
        {
            _repository.Setup(r => r.GetOwnerUserIdAsync(9)).ReturnsAsync((string?)null);

            Func<Task> act = () => _service.DismissAsync(9, null, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<NotFoundException>();
        }
    }
}
