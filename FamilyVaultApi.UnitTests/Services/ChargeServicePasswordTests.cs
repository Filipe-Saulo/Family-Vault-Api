using System.Security;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
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
    // PDF com senha: informar a senha (abre na hora) e informar o valor (despesa sem abrir o PDF)
    public class ChargeServicePasswordTests
    {
        private const string UserId = "user-1";
        private const string Sender = "faturas@banco.com.br";
        private const string Password = "12345";

        private readonly Mock<IChargeRepository> _repository = new();
        private readonly Mock<IGmailRepository> _gmailRepository = new();
        private readonly Mock<ITransactionService> _transactionService = new();
        private readonly Mock<IGmailApiClient> _client = new();
        private readonly ChargeProcessingOptions _options = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
        private readonly GmailTokenProtector _protector;
        private readonly ChargeService _service;
        private readonly byte[] _locked = TestPdfs.Create("fatura-senha", userPassword: Password);

        public ChargeServicePasswordTests()
        {
            _protector = new GmailTokenProtector(Options.Create(new GmailOptions { TokenEncryptionKey = Convert.ToBase64String(new byte[32]) }));
            _gmailRepository.Setup(r => r.GetConnectionAsync(UserId)).ReturnsAsync(new GmailConnection
            {
                UserId = UserId,
                Status = GmailConnectionStatus.Active,
                RefreshTokenEncrypted = _protector.Protect("rt")
            });
            _client.Setup(c => c.GetAccessTokenAsync("rt", It.IsAny<CancellationToken>())).ReturnsAsync("at");

            _service = NewService(withGmail: true);

            _repository.Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns((Func<Task> action) => action());
            _repository.Setup(r => r.TryReservePasswordAttemptAsync(1, 5)).ReturnsAsync(true);
            _repository.Setup(r => r.TryRequeueUnlockedDocumentAsync(1, It.IsAny<byte[]>())).ReturnsAsync(true);
        }

        private ChargeService NewService(bool withGmail) => withGmail
            ? new ChargeService(_repository.Object, _gmailRepository.Object, _transactionService.Object, Options.Create(_options), _time,
                new GmailDocumentFetcher(_gmailRepository.Object, _client.Object, _protector), _protector)
            : new ChargeService(_repository.Object, _gmailRepository.Object, _transactionService.Object, Options.Create(_options), _time);

        private InboundDocument SetupDocument(InboundDocumentSource source = InboundDocumentSource.Upload, string owner = UserId,
            InboundDocumentStatus status = InboundDocumentStatus.PasswordRequired, int passwordAttempts = 0, string? sender = null)
        {
            var document = new InboundDocument
            {
                InboundDocumentId = 1,
                UserId = owner,
                Source = source,
                ExternalId = source == InboundDocumentSource.Gmail ? "gmail:msg-1:2" : "upload:abc",
                Sender = sender,
                Status = status,
                PasswordAttempts = passwordAttempts,
                TemporaryContent = source == InboundDocumentSource.Upload ? _locked : null,
                ContentType = "application/pdf"
            };
            _repository.Setup(r => r.GetDocumentAsync(1)).ReturnsAsync(document);
            // Como o UPDATE condicional: a reserva gasta a tentativa no documento
            _repository.Setup(r => r.TryReservePasswordAttemptAsync(1, 5))
                .Callback(() => document.PasswordAttempts++)
                .ReturnsAsync(true);
            return document;
        }

        private Task<Models.Dto.Responses.Charge.ChargeUploadResponseDto> SubmitAsync(string password, bool remember = false, ChargeService? service = null) =>
            (service ?? _service).SubmitDocumentPasswordAsync(1, new SubmitDocumentPasswordDto { Password = password, RememberForSender = remember },
                ClaimsPrincipalTestHelper.CreateUser(UserId), CancellationToken.None);

        // ---------- Informar a senha ----------

        [Fact]
        public async Task SubmitDocumentPasswordAsync_RightPasswordOnUpload_ShouldRequeueWithTheUnlockedPdf()
        {
            SetupDocument();
            byte[]? requeued = null;
            _repository.Setup(r => r.TryRequeueUnlockedDocumentAsync(1, It.IsAny<byte[]>()))
                .Callback((int _, byte[] content) => requeued = content)
                .ReturnsAsync(true);

            var result = await SubmitAsync(Password, remember: true);

            result.DocumentId.Should().Be(1);
            TestPdfs.IsEncrypted(requeued!).Should().BeFalse();
            TestPdfs.ReadPageContent(requeued!).Should().Contain("fatura-senha");
            // Upload não tem remetente: rememberForSender não grava nada
            _repository.Verify(r => r.SaveSenderPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_WrongPassword_ShouldSpendAnAttemptAndTellHowManyRemain()
        {
            SetupDocument(passwordAttempts: 1);

            var act = () => SubmitAsync("00000");

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Senha incorreta. Restam 3 tentativa(s).");
            _repository.Verify(r => r.TryReservePasswordAttemptAsync(1, 5), Times.Once);
            _repository.Verify(r => r.TryRequeueUnlockedDocumentAsync(It.IsAny<int>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_LastAttemptWrong_ShouldPointToInformingTheValue()
        {
            SetupDocument(passwordAttempts: 4);

            var act = () => SubmitAsync("00000");

            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Senha incorreta. Limite de tentativas de senha atingido. Informe o valor da conta.");
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_AttemptsExhausted_ShouldReturnTooManyRequestsWithoutTrying()
        {
            SetupDocument(passwordAttempts: 5);

            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _repository.Verify(r => r.TryReservePasswordAttemptAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_ReservationLostToAnotherRequest_ShouldReturnTooManyRequests()
        {
            SetupDocument(passwordAttempts: 4);
            _repository.Setup(r => r.TryReservePasswordAttemptAsync(1, 5)).ReturnsAsync(false);

            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _repository.Verify(r => r.TryRequeueUnlockedDocumentAsync(It.IsAny<int>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_DocumentOfAnotherUser_ShouldThrowSecurityException()
        {
            SetupDocument(owner: "other-user");

            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_UnknownDocument_ShouldThrowNotFound()
        {
            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Theory]
        [InlineData(InboundDocumentStatus.Pending)]
        [InlineData(InboundDocumentStatus.Done)]
        [InlineData(InboundDocumentStatus.Failed)]
        public async Task SubmitDocumentPasswordAsync_DocumentNotWaitingPassword_ShouldThrowConflict(InboundDocumentStatus status)
        {
            SetupDocument(status: status);

            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<ConflictException>();
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_GmailWithRemember_ShouldDownloadAgainAndSaveTheEncryptedPasswordForTheSender()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ReturnsAsync(_locked);
            string? saved = null;
            _repository.Setup(r => r.SaveSenderPasswordAsync(UserId, Sender, It.IsAny<string>(), _time.Now.UtcDateTime))
                .Callback((string _, string _, string encrypted, DateTime _) => saved = encrypted)
                .Returns(Task.CompletedTask);

            await SubmitAsync(Password, remember: true);

            _repository.Verify(r => r.TryRequeueUnlockedDocumentAsync(1, It.IsAny<byte[]>()), Times.Once);
            saved.Should().NotBe(Password);
            _protector.Unprotect(saved!).Should().Be(Password);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_GmailWithoutRemember_ShouldNotSavePassword()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ReturnsAsync(_locked);

            await SubmitAsync(Password);

            _repository.Verify(r => r.SaveSenderPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_GmailNotConfigured_ShouldThrowBadRequest()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);

            var act = () => SubmitAsync(Password, service: NewService(withGmail: false));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("A integração com o Gmail não está configurada.");
            _repository.Verify(r => r.TryReservePasswordAttemptAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_GmailRevoked_ShouldAskToReconnectWithoutSpendingAttempt()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);
            _client.Setup(c => c.GetAccessTokenAsync("rt", It.IsAny<CancellationToken>())).ThrowsAsync(new GmailAccessRevokedException());

            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Reconecte o Gmail para informar a senha.");
            _repository.Verify(r => r.TryReservePasswordAttemptAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SubmitDocumentPasswordAsync_EmailDeleted_ShouldFailTheDocumentAndThrowConflict()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ThrowsAsync(new GmailMessageNotFoundException());

            var act = () => SubmitAsync(Password);

            await act.Should().ThrowAsync<ConflictException>().WithMessage("O e-mail não está mais disponível no Gmail.");
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "O e-mail não está mais disponível no Gmail.", null), Times.Once);
        }

        // ---------- Informar o valor ----------

        private static ManualChargeDto ManualBody(string? issuer = null) => new()
        {
            Description = "Fatura do cartão",
            Amount = 512.30m,
            DueDate = new DateTime(2026, 11, 25),
            CategoryId = 2,
            Issuer = issuer
        };

        [Fact]
        public async Task CreateManualChargeAsync_ShouldCreateExpenseOnDueDateAndConfirmedChargeWithSenderAsIssuer()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);
            CreateTransactionDto? createdWith = null;
            _transactionService.Setup(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Callback((CreateTransactionDto dto, System.Security.Claims.ClaimsPrincipal _) => createdWith = dto)
                .ReturnsAsync(new TransactionResponseDto { TransactionId = 40 });
            Charge? charge = null;
            _repository.Setup(r => r.TryCompleteManuallyAsync(1, It.IsAny<Charge>()))
                .Callback((int _, Charge c) => charge = c)
                .ReturnsAsync(true);

            var result = await _service.CreateManualChargeAsync(1, ManualBody(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.TransactionId.Should().Be(40);
            createdWith!.UserId.Should().Be(UserId);
            createdWith.TransactionTypeId.Should().Be((int)TransactionTypeCode.Expense);
            createdWith.TransactionDate.Should().Be(new DateTime(2026, 11, 25));
            createdWith.Amount.Should().Be(512.30m);
            charge!.Status.Should().Be(ChargeStatus.Confirmed);
            charge.TransactionId.Should().Be(40);
            charge.Issuer.Should().Be(Sender);
            charge.Amount.Should().Be(512.30m);
            charge.DueDate.Should().Be(new DateTime(2026, 11, 25));
            charge.UserId.Should().Be(UserId);
        }

        [Fact]
        public async Task CreateManualChargeAsync_WithIssuer_ShouldUseIt()
        {
            SetupDocument(InboundDocumentSource.Gmail, sender: Sender);
            _transactionService.Setup(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(new TransactionResponseDto { TransactionId = 41 });
            Charge? charge = null;
            _repository.Setup(r => r.TryCompleteManuallyAsync(1, It.IsAny<Charge>()))
                .Callback((int _, Charge c) => charge = c)
                .ReturnsAsync(true);

            await _service.CreateManualChargeAsync(1, ManualBody("Banco X"), ClaimsPrincipalTestHelper.CreateUser(UserId));

            charge!.Issuer.Should().Be("Banco X");
        }

        [Fact]
        public async Task CreateManualChargeAsync_DocumentTreatedMeanwhile_ShouldThrowConflict()
        {
            SetupDocument();
            _transactionService.Setup(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(new TransactionResponseDto { TransactionId = 42 });
            _repository.Setup(r => r.TryCompleteManuallyAsync(1, It.IsAny<Charge>())).ReturnsAsync(false);

            var act = () => _service.CreateManualChargeAsync(1, ManualBody(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
        }

        [Fact]
        public async Task CreateManualChargeAsync_DocumentNotWaitingPassword_ShouldThrowConflictWithoutCreatingExpense()
        {
            SetupDocument(status: InboundDocumentStatus.Failed);

            var act = () => _service.CreateManualChargeAsync(1, ManualBody(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
            _transactionService.Verify(t => t.CreateAsync(It.IsAny<CreateTransactionDto>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>()), Times.Never);
        }

        [Fact]
        public async Task CreateManualChargeAsync_DocumentOfAnotherUser_ShouldThrowSecurityException()
        {
            SetupDocument(owner: "other-user");

            var act = () => _service.CreateManualChargeAsync(1, ManualBody(), ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<SecurityException>();
        }
    }
}
