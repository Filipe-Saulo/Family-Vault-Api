using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // O worker fica desligado no factory: o processamento é acionado chamando o ChargeDocumentProcessor
    // num scope, com o extrator falso (FakeChargeExtractor). Cada teste usa um marcador único nos bytes,
    // então o sha256 (e o documento) de cada teste é próprio.
    public class ChargeTests : IClassFixture<ApiFixture>
    {
        // Seeds do DatabaseContext: categoria 2 é de despesa, categoria 1 é de receita
        private const int ExpenseCategoryId = 2;
        private const int IncomeCategoryId = 1;
        private const int ExpenseType = 1;
        private const int ExpensePurposeId = 1;
        private const int MaxUploadBytes = 10 * 1024 * 1024;

        private readonly ApiFixture _api;

        public ChargeTests(ApiFixture api) => _api = api;

        private FakeChargeExtractor Extractor => _api.Factory.ChargeExtractor;

        // ---------- Upload ----------

        [Fact]
        public async Task Upload_ValidPdf_ShouldReturnAcceptedWithDocumentId()
        {
            var (_, user) = await _api.UserClientAsync(0);

            var response = await UploadAsync(user, PdfBytes(NewMarker()));

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var data = (await response.ReadEnvelopeAsync<UploadData>()).Data!;
            data.DocumentId.Should().BePositive();
            var document = await GetDocumentAsync(data.DocumentId);
            document.Status.Should().Be(InboundDocumentStatus.Pending);
            document.Source.Should().Be(InboundDocumentSource.Upload);
        }

        [Fact]
        public async Task Upload_WithoutToken_ShouldReturnUnauthorized()
        {
            var response = await UploadAsync(_api.AnonymousClient(), PdfBytes(NewMarker()));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task List_WithoutToken_ShouldReturnUnauthorized()
        {
            var response = await _api.AnonymousClient().GetAsync("/api/charge");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Upload_WithoutFileField_ShouldReturnBadRequest()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var content = new MultipartFormDataContent { { new StringContent("x"), "outro" } };

            var response = await user.PostAsync("/api/charge/upload", content);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ReadEnvelopeAsync<object>()).TraceId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Upload_EmptyFile_ShouldReturnBadRequest()
        {
            var (_, user) = await _api.UserClientAsync(0);

            var response = await UploadAsync(user, []);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Upload_TextWithPdfContentType_ShouldReturnBadRequest()
        {
            // O tipo vale pelos bytes iniciais, não pelo header
            var (_, user) = await _api.UserClientAsync(0);

            var response = await UploadAsync(user, Encoding.UTF8.GetBytes("isto é só texto " + NewMarker()), "application/pdf");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Upload_AboveMaxSize_ShouldReturnBadRequest()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var bytes = new byte[MaxUploadBytes + 1];
            Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(bytes, 0);

            var response = await UploadAsync(user, bytes);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Upload_BodyAboveRequestLimit_ShouldBeRejectedInEnvelope()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var bytes = new byte[12 * 1024 * 1024];
            Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(bytes, 0);

            var response = await UploadAsync(user, bytes);

            // No Kestrel o [RequestSizeLimit] corta antes (413); o TestServer não aplica esse limite e o controller
            // recusa pelo tamanho do arquivo (400). Nos dois casos, nunca 500.
            response.StatusCode.Should().BeOneOf(HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.BadRequest);
            var envelope = await response.ReadEnvelopeAsync<string>();
            envelope.TraceId.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]
        [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A })]
        public async Task Upload_JpegOrPngByBytes_ShouldBeAccepted(string contentType, byte[] header)
        {
            var (_, user) = await _api.UserClientAsync(0);
            var bytes = header.Concat(Encoding.ASCII.GetBytes(NewMarker())).ToArray();

            var response = await UploadAsync(user, bytes, contentType);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task Upload_SameFileTwice_ShouldReturnSameDocumentWithoutNewOne()
        {
            var (userId, user) = await _api.UserClientAsync(0);
            var bytes = PdfBytes(NewMarker());

            var first = await UploadDocumentAsync(user, bytes);
            var second = await UploadDocumentAsync(user, bytes);

            second.Should().Be(first);
            using var scope = _api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var externalId = $"upload:{Sha256Hex(bytes)}";
            (await db.InboundDocuments.CountAsync(d => d.UserId == userId && d.ExternalId == externalId)).Should().Be(1);
        }

        [Fact]
        public async Task Upload_OverPerUserDailyLimit_ShouldReturnTooManyRequests()
        {
            using var factory = new FamilyVaultApiFactory(ApiFixture.AdminEmail, ApiFixture.AdminPassword,
                settings: new Dictionary<string, string?> { ["Charges:DailyLimits:PerUser"] = "2" });
            var client = await LoginAdminAsync(factory);

            (await UploadAsync(client, PdfBytes(NewMarker()))).StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await UploadAsync(client, PdfBytes(NewMarker()))).StatusCode.Should().Be(HttpStatusCode.Accepted);
            var third = await UploadAsync(client, PdfBytes(NewMarker()));

            third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await third.ReadEnvelopeAsync<object>()).TraceId.Should().NotBeNullOrWhiteSpace();
        }

        // ---------- Fluxo feliz: upload -> processa -> lista -> detalhe -> confirm ----------

        [Fact]
        public async Task Flow_UploadProcessListDetailConfirm_ShouldCreateExpenseOnDueDate()
        {
            var (userId, user) = await _api.UserClientAsync(0);
            var marker = NewMarker();
            var due = DueIn(20);
            var barcode = BankLine(Seed());
            Extractor.Returns(marker, Charge(marker, 321.45m, due, barcode, ExpenseCategoryId));

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            var listed = (await ListAsync(user)).Single(c => c.DocumentId == documentId);
            listed.Status.Should().Be("PendingReview");
            listed.Source.Should().Be("Upload");
            listed.Amount.Should().Be(321.45m);
            listed.DueDate!.Value.Date.Should().Be(due);
            listed.Barcode.Should().Be(barcode);
            listed.SuggestedCategoryId.Should().Be(ExpenseCategoryId);
            listed.Issuer.Should().Be("Energia SA");
            listed.PossibleDuplicateTransactionId.Should().BeNull();
            listed.TransactionId.Should().BeNull();
            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.Done);

            var detail = await GetChargeAsync(user, listed.ChargeId);
            detail.DocumentId.Should().Be(documentId);

            var confirm = await user.PostAsJsonAsync($"/api/charge/{listed.ChargeId}/confirm", new
            {
                description = "Conta de luz",
                amount = 330.10m,
                dueDate = due.AddDays(1),
                categoryId = ExpenseCategoryId
            });
            confirm.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = (await confirm.ReadEnvelopeAsync<TransactionData>()).Data!;
            created.UserId.Should().Be(userId);
            created.Amount.Should().Be(330.10m);

            var transaction = (await ListTransactionsAsync(user)).Single(t => t.TransactionId == created.TransactionId);
            transaction.TransactionDate.Date.Should().Be(due.AddDays(1));
            transaction.Description.Should().Be("Conta de luz");
            transaction.Amount.Should().Be(330.10m);

            var after = await GetChargeAsync(user, listed.ChargeId);
            after.Status.Should().Be("Confirmed");
            after.TransactionId.Should().Be(created.TransactionId);
        }

        [Fact]
        public async Task Confirm_Twice_ShouldReturnConflictAndKeepSingleTransaction()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);
            var body = ConfirmBody(DueIn(10));

            (await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", body)).StatusCode.Should().Be(HttpStatusCode.Created);
            var before = (await ListTransactionsAsync(user)).Count;

            var again = await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", body);

            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ListTransactionsAsync(user)).Count.Should().Be(before);
        }

        [Fact]
        public async Task Confirm_AfterDismiss_ShouldReturnConflict()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);
            (await user.PostAsync($"/api/charge/{charge.ChargeId}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", ConfirmBody(DueIn(5)));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Dismiss_PendingCharge_ShouldReturnNoContentAndThenConflict()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);

            var dismiss = await user.PostAsync($"/api/charge/{charge.ChargeId}/dismiss", null);

            dismiss.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetChargeAsync(user, charge.ChargeId)).Status.Should().Be("Dismissed");
            (await user.PostAsync($"/api/charge/{charge.ChargeId}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Dismiss_WithIgnoreSenderBody_ShouldReturnNoContent()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);

            var dismiss = await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/dismiss", new { ignoreSender = true });

            dismiss.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Dismiss_AfterConfirm_ShouldReturnConflict()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);
            (await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", ConfirmBody(DueIn(5)))).StatusCode
                .Should().Be(HttpStatusCode.Created);

            var response = await user.PostAsync($"/api/charge/{charge.ChargeId}/dismiss", null);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Charge_OfAnotherUser_ShouldBeForbiddenForDetailConfirmAndDismiss()
        {
            var (_, owner) = await _api.UserClientAsync(0);
            var (_, other) = await _api.UserClientAsync(1);
            var charge = await NewPendingChargeAsync(owner);

            (await other.GetAsync($"/api/charge/{charge.ChargeId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await other.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", ConfirmBody(DueIn(5)))).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await other.PostAsync($"/api/charge/{charge.ChargeId}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // A charge continua pendente e fora da lista do outro usuário
            (await ListAsync(other)).Should().NotContain(c => c.ChargeId == charge.ChargeId);
            (await GetChargeAsync(owner, charge.ChargeId)).Status.Should().Be("PendingReview");
        }

        [Fact]
        public async Task Charge_OfAnotherUser_ShouldBeForbiddenEvenForAdministrator()
        {
            var (_, owner) = await _api.UserClientAsync(0);
            var admin = await _api.AdminClientAsync();
            var charge = await NewPendingChargeAsync(owner);

            (await admin.GetAsync($"/api/charge/{charge.ChargeId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await admin.PostAsync($"/api/charge/{charge.ChargeId}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Charge_UnknownId_ShouldReturnNotFound()
        {
            var (_, user) = await _api.UserClientAsync(0);

            (await user.GetAsync("/api/charge/99999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await user.PostAsJsonAsync("/api/charge/99999999/confirm", ConfirmBody(DueIn(5)))).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
            (await user.PostAsync("/api/charge/99999999/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Confirm_WithInvalidBody_ShouldReturnBadRequestAndKeepChargePending()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);
            var due = DueIn(5);

            var invalidBodies = new Dictionary<string, object>
            {
                ["descrição vazia"] = new { description = "", amount = 10m, dueDate = due, categoryId = ExpenseCategoryId },
                ["descrição longa"] = new { description = new string('a', 501), amount = 10m, dueDate = due, categoryId = ExpenseCategoryId },
                ["valor zero"] = new { description = "Ok", amount = 0m, dueDate = due, categoryId = ExpenseCategoryId },
                ["valor acima do teto"] = new { description = "Ok", amount = 1000000000m, dueDate = due, categoryId = ExpenseCategoryId },
                ["sem vencimento"] = new { description = "Ok", amount = 10m, categoryId = ExpenseCategoryId },
                ["categoria zero"] = new { description = "Ok", amount = 10m, dueDate = due, categoryId = 0 },
            };

            foreach (var (name, body) in invalidBodies)
            {
                var response = await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", body);
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: name);
            }

            (await GetChargeAsync(user, charge.ChargeId)).Status.Should().Be("PendingReview");
        }

        [Fact]
        public async Task Confirm_WithIncomeCategory_ShouldReturnBadRequestAndKeepChargePending()
        {
            // A categoria passa pelas regras que já existem para transações
            var (_, user) = await _api.UserClientAsync(0);
            var charge = await NewPendingChargeAsync(user);

            var response = await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", new
            {
                description = "Categoria errada",
                amount = 10m,
                dueDate = DueIn(5),
                categoryId = IncomeCategoryId
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetChargeAsync(user, charge.ChargeId)).Status.Should().Be("PendingReview");
        }

        // ---------- Lista ----------

        [Fact]
        public async Task List_FilteredByStatus_ShouldReturnOnlyThatStatus()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var toDismiss = await NewPendingChargeAsync(user);
            var pending = await NewPendingChargeAsync(user);
            (await user.PostAsync($"/api/charge/{toDismiss.ChargeId}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dismissed = await ListAsync(user, "status=Dismissed");
            var pendingList = await ListAsync(user, "status=PendingReview");

            dismissed.Should().Contain(c => c.ChargeId == toDismiss.ChargeId);
            dismissed.Should().NotContain(c => c.ChargeId == pending.ChargeId);
            dismissed.Should().OnlyContain(c => c.Status == "Dismissed");
            pendingList.Should().Contain(c => c.ChargeId == pending.ChargeId);
            pendingList.Should().NotContain(c => c.ChargeId == toDismiss.ChargeId);
            pendingList.Should().OnlyContain(c => c.Status == "PendingReview");
        }

        [Fact]
        public async Task List_ShouldShowOnlyOwnChargesNewestFirst()
        {
            var (_, owner) = await _api.UserClientAsync(0);
            var (_, other) = await _api.UserClientAsync(1);
            var first = await NewPendingChargeAsync(owner);
            var second = await NewPendingChargeAsync(owner);
            var foreign = await NewPendingChargeAsync(other);

            var items = await ListAsync(owner);

            items.Should().NotContain(c => c.ChargeId == foreign.ChargeId);
            items.FindIndex(c => c.ChargeId == second.ChargeId).Should().BeLessThan(items.FindIndex(c => c.ChargeId == first.ChargeId));
        }

        // ---------- Processamento ----------

        [Fact]
        public async Task Process_WhenExtractorSaysNotACharge_ShouldEndDoneWithoutChargeOrFailure()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var marker = NewMarker();
            Extractor.Returns(marker, new ChargeExtractionResult { IsCharge = false });

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            (await ListAsync(user)).Should().NotContain(c => c.DocumentId == documentId);
            (await FailuresAsync(user)).Should().NotContain(f => f.DocumentId == documentId);
            var document = await GetDocumentAsync(documentId);
            document.Status.Should().Be(InboundDocumentStatus.Done);
            document.TemporaryContent.Should().BeNull();
            Extractor.CallsFor(marker).Should().Be(1);
        }

        [Fact]
        public async Task Process_WhenExtractorThrowsChargeExtractionException_ShouldShowReasonInFailuresWithoutRetry()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var marker = NewMarker();
            Extractor.Throws(marker, new ChargeExtractionException("Imagem ilegível."));

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            var failure = (await FailuresAsync(user)).Single(f => f.DocumentId == documentId);
            failure.FailureReason.Should().Be("Imagem ilegível.");
            failure.Source.Should().Be("Upload");
            Extractor.CallsFor(marker).Should().Be(1);
            (await ListAsync(user)).Should().NotContain(c => c.DocumentId == documentId);
            (await GetDocumentAsync(documentId)).TemporaryContent.Should().BeNull();
        }

        [Fact]
        public void Services_WithoutExtractor_ShouldPassDevelopmentDiValidation()
        {
            // Até o lote Evals não há IChargeExtractor real: a API tem de subir mesmo assim (Development valida a DI no Build)
            IServiceCollection? captured = null;
            using var factory = new FamilyVaultApiFactory(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
            using var withoutExtractor = factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChargeExtractor>();
                    captured = services;
                }));
            withoutExtractor.CreateClient().Dispose();
            captured!.Should().NotContain(d => d.ServiceType == typeof(IChargeExtractor));
            captured!.Should().Contain(d => d.ServiceType == typeof(ChargeDocumentProcessor));

            Action act = () =>
            {
                using var provider = captured!.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            };

            act.Should().NotThrow();
        }

        [Fact]
        public async Task Upload_SameFileOfFailedDocument_ShouldRequeueAndProcessAgain()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var marker = NewMarker();
            var bytes = PdfBytes(marker);
            Extractor.Throws(marker, new ChargeExtractionException("Imagem ilegível."));

            var documentId = await UploadDocumentAsync(user, bytes);
            await ProcessAllAsync();
            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.Failed);

            Extractor.Returns(marker, Charge(marker, 87.90m, DueIn(10), null, ExpenseCategoryId));
            (await UploadDocumentAsync(user, bytes)).Should().Be(documentId);

            var requeued = await GetDocumentAsync(documentId);
            requeued.Status.Should().Be(InboundDocumentStatus.Pending);
            requeued.Attempts.Should().Be(0);
            requeued.FailureReason.Should().BeNull();
            requeued.TemporaryContent.Should().NotBeNull();
            (await FailuresAsync(user)).Should().NotContain(f => f.DocumentId == documentId);

            await ProcessAllAsync();

            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.Done);
            (await ListAsync(user)).Single(c => c.DocumentId == documentId).Amount.Should().Be(87.90m);
            Extractor.CallsFor(marker).Should().Be(2);
        }

        [Fact]
        public async Task Process_WhenPdfIsEncrypted_ShouldFailWithPasswordMessageWithoutCallingExtractor()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var marker = NewMarker();
            var bytes = Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj\n<< /Filter /Standard /Encrypt 5 0 R >>\n% {marker}\n");

            var documentId = await UploadDocumentAsync(user, bytes);
            await ProcessAllAsync();

            var failure = (await FailuresAsync(user)).Single(f => f.DocumentId == documentId);
            failure.FailureReason.Should().Be("O PDF está protegido por senha.");
            Extractor.CallsFor(marker).Should().Be(0);
        }

        [Fact]
        public async Task Process_WhenExtractorThrowsGenericErrorThreeTimes_ShouldRetryThenFail()
        {
            var (_, user) = await _api.UserClientAsync(0);
            await ProcessAllAsync(); // fila vazia, para o documento do teste ser sempre o próximo
            var marker = NewMarker();
            Extractor.Throws(marker, new InvalidOperationException("falha qualquer"));

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            using var scope = _api.Factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>();

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                (await processor.ProcessNextAsync(CancellationToken.None)).Should().BeTrue();
                var document = await GetDocumentAsync(documentId);
                document.Status.Should().Be(InboundDocumentStatus.Pending, because: $"a tentativa {attempt} ainda tem retentativa");
                document.Attempts.Should().Be(attempt);
                (await FailuresAsync(user)).Should().NotContain(f => f.DocumentId == documentId);
            }

            (await processor.ProcessNextAsync(CancellationToken.None)).Should().BeTrue();

            var failure = (await FailuresAsync(user)).Single(f => f.DocumentId == documentId);
            failure.FailureReason.Should().Be("Não foi possível processar o documento.");
            Extractor.CallsFor(marker).Should().Be(3);
            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.Failed);
            (await processor.ProcessNextAsync(CancellationToken.None)).Should().BeFalse();
        }

        [Fact]
        public async Task Process_WhenExtractorReturnsInvalidFields_ShouldKeepThemNull()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var marker = NewMarker();
            Extractor.Returns(marker, new ChargeExtractionResult
            {
                IsCharge = true,
                Issuer = "  Fornecedor  ",
                Description = marker,
                Amount = 0m,
                DueDate = DateTime.UtcNow.Date.AddYears(3),
                Barcode = "12345678",
                SuggestedCategoryId = IncomeCategoryId
            });

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            var charge = (await ListAsync(user)).Single(c => c.DocumentId == documentId);
            charge.Amount.Should().BeNull();
            charge.DueDate.Should().BeNull();
            charge.Barcode.Should().BeNull();
            charge.SuggestedCategoryId.Should().BeNull();
            charge.Issuer.Should().Be("Fornecedor");
            charge.Status.Should().Be("PendingReview");
        }

        [Fact]
        public async Task Process_WhenSuggestedCategoryBelongsToAnotherUser_ShouldKeepItNull()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var (_, other) = await _api.UserClientAsync(1);
            var created = await other.PostAsJsonAsync("/api/category",
                new { description = $"Pessoal {NewMarker()}", categoryPurposeId = ExpensePurposeId });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var foreignCategoryId = (await created.ReadEnvelopeAsync<CategoryData>()).Data!.CategoryId;
            var marker = NewMarker();
            Extractor.Returns(marker, Charge(marker, 55.5m, DueIn(7), null, foreignCategoryId));

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            var charge = (await ListAsync(user)).Single(c => c.DocumentId == documentId);
            charge.SuggestedCategoryId.Should().BeNull();
            charge.Amount.Should().Be(55.5m);
        }

        [Fact]
        public async Task Process_SameBarcodeInTwoUploads_ShouldCreateOnlyOneCharge()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var barcode = BankLine(Seed());
            var markerA = NewMarker();
            var markerB = NewMarker();
            Extractor.Returns(markerA, Charge(markerA, 100m, DueIn(9), barcode, ExpenseCategoryId));
            Extractor.Returns(markerB, Charge(markerB, 100m, DueIn(9), barcode, ExpenseCategoryId));

            var docA = await UploadDocumentAsync(user, PdfBytes(markerA));
            await ProcessAllAsync();
            var docB = await UploadDocumentAsync(user, PdfBytes(markerB));
            await ProcessAllAsync();

            docB.Should().NotBe(docA);
            var charges = (await ListAsync(user)).Where(c => c.Barcode == barcode).ToList();
            charges.Should().ContainSingle().Which.DocumentId.Should().Be(docA);
            (await GetDocumentAsync(docB)).Status.Should().Be(InboundDocumentStatus.Done);
        }

        [Fact]
        public async Task Process_WithExpenseTwoDaysAwayWithSameAmount_ShouldFlagPossibleDuplicate()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var due = DueIn(30);
            var transaction = await CreateExpenseAsync(user, 4321.12m, due.AddDays(-2));
            var marker = NewMarker();
            Extractor.Returns(marker, Charge(marker, 4321.12m, due, null, ExpenseCategoryId));

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            var charge = (await ListAsync(user)).Single(c => c.DocumentId == documentId);
            charge.PossibleDuplicateTransactionId.Should().Be(transaction.TransactionId);
            charge.Status.Should().Be("PendingReview"); // só avisa
        }

        [Fact]
        public async Task Process_WithExpenseFourDaysAwayWithSameAmount_ShouldNotFlagDuplicate()
        {
            var (_, user) = await _api.UserClientAsync(0);
            var due = DueIn(40);
            await CreateExpenseAsync(user, 4322.34m, due.AddDays(4));
            var marker = NewMarker();
            Extractor.Returns(marker, Charge(marker, 4322.34m, due, null, ExpenseCategoryId));

            var documentId = await UploadDocumentAsync(user, PdfBytes(marker));
            await ProcessAllAsync();

            (await ListAsync(user)).Single(c => c.DocumentId == documentId).PossibleDuplicateTransactionId.Should().BeNull();
        }

        // ---------- Helpers ----------

        private static string NewMarker() => $"M{Guid.NewGuid():N}";

        private static int _seed = 100000;
        private static int Seed() => Interlocked.Increment(ref _seed);

        private static DateTime DueIn(int days) => DateTime.UtcNow.Date.AddDays(days);

        private static byte[] PdfBytes(string marker) => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {marker}\n");

        private static string Sha256Hex(byte[] bytes) =>
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        private static ChargeExtractionResult Charge(string marker, decimal amount, DateTime due, string? barcode, int? categoryId) => new()
        {
            IsCharge = true,
            Issuer = "Energia SA",
            Description = marker,
            Amount = amount,
            DueDate = due,
            Barcode = barcode,
            SuggestedCategoryId = categoryId
        };

        private static object ConfirmBody(DateTime due) => new
        {
            description = "Conta confirmada",
            amount = 150.25m,
            dueDate = due,
            categoryId = ExpenseCategoryId
        };

        private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string contentType = "application/pdf")
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(file, "file", "conta.pdf");
            return client.PostAsync("/api/charge/upload", content);
        }

        private static async Task<int> UploadDocumentAsync(HttpClient client, byte[] bytes, string contentType = "application/pdf")
        {
            var response = await UploadAsync(client, bytes, contentType);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            return (await response.ReadEnvelopeAsync<UploadData>()).Data!.DocumentId;
        }

        // Sobe um documento com uma charge válida e processa; devolve a charge pendente
        private async Task<ChargeData> NewPendingChargeAsync(HttpClient client)
        {
            var marker = NewMarker();
            Extractor.Returns(marker, Charge(marker, 150.25m, DueIn(12), BankLine(Seed()), ExpenseCategoryId));
            var documentId = await UploadDocumentAsync(client, PdfBytes(marker));
            await ProcessAllAsync();
            return (await ListAsync(client)).Single(c => c.DocumentId == documentId);
        }

        private async Task ProcessAllAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>();
            var processed = 0;
            while (await processor.ProcessNextAsync(CancellationToken.None))
                (++processed).Should().BeLessThan(500, because: "o processamento deve esgotar a fila");
        }

        private async Task<InboundDocument> GetDocumentAsync(int documentId)
        {
            using var scope = _api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            return await db.InboundDocuments.AsNoTracking().SingleAsync(d => d.InboundDocumentId == documentId);
        }

        private static async Task<HttpClient> LoginAdminAsync(FamilyVaultApiFactory factory)
        {
            var client = factory.CreateClient();
            var login = await client.PostAsJsonAsync("/api/app/login",
                new { email = ApiFixture.AdminEmail, password = ApiFixture.AdminPassword });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var token = (await login.ReadEnvelopeAsync<LoginData>()).Data!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static async Task<List<ChargeData>> ListAsync(HttpClient client, string? query = null)
        {
            var response = await client.GetAsync($"/api/charge?PageSize=100{(query is null ? "" : "&" + query)}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = (await response.ReadEnvelopeAsync<PagedData<ChargeData>>()).Data!;
            page.TotalCount.Should().Be(page.Items.Count);
            return page.Items;
        }

        private static async Task<ChargeData> GetChargeAsync(HttpClient client, int chargeId)
        {
            var response = await client.GetAsync($"/api/charge/{chargeId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<ChargeData>()).Data!;
        }

        private static async Task<List<FailureData>> FailuresAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/charge/failures?PageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<PagedData<FailureData>>()).Data!.Items;
        }

        private static async Task<TransactionData> CreateExpenseAsync(HttpClient client, decimal amount, DateTime date)
        {
            var response = await client.PostAsJsonAsync("/api/transaction", new
            {
                categoryId = ExpenseCategoryId,
                transactionTypeId = ExpenseType,
                description = $"Despesa {NewMarker()}",
                amount,
                transactionDate = date
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.ReadEnvelopeAsync<TransactionData>()).Data!;
        }

        private static async Task<List<TransactionData>> ListTransactionsAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/transaction?PageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = (await response.ReadEnvelopeAsync<PagedData<TransactionData>>()).Data!;
            page.TotalCount.Should().Be(page.Items.Count);
            return page.Items;
        }

        // Linha digitável bancária de 47 dígitos com DVs corretos (módulo 10 nos campos, módulo 11 no geral)
        private static string BankLine(int seed)
        {
            var free25 = "1" + seed.ToString().PadLeft(24, '0');
            const string factorAndValue = "9876" + "0000012345";
            var barcode43 = "3419" + factorAndValue + free25;

            var sum = 0;
            var weight = 2;
            for (var i = barcode43.Length - 1; i >= 0; i--)
            {
                sum += (barcode43[i] - '0') * weight;
                weight = weight == 9 ? 2 : weight + 1;
            }
            var general = 11 - sum % 11;
            if (general is 0 or 10 or 11) general = 1;

            var field1 = "3419" + free25[..5];
            var field2 = free25.Substring(5, 10);
            var field3 = free25.Substring(15, 10);
            return field1 + Mod10(field1) + field2 + Mod10(field2) + field3 + Mod10(field3) + general + factorAndValue;
        }

        private static int Mod10(string digits)
        {
            var sum = 0;
            var weight = 2;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var product = (digits[i] - '0') * weight;
                sum += product > 9 ? product - 9 : product;
                weight = weight == 2 ? 1 : 2;
            }
            return (10 - sum % 10) % 10;
        }

        private sealed record UploadData(int DocumentId);

        private sealed record LoginData(string Token);

        private sealed record PagedData<T>(int TotalCount, List<T> Items);

        private sealed record ChargeData(
            int ChargeId, int DocumentId, string Source, string? Issuer, string? Description, decimal? Amount,
            DateTime? DueDate, string? Barcode, int? SuggestedCategoryId, int? PossibleDuplicateTransactionId,
            string Status, int? TransactionId, DateTime CreatedAt);

        private sealed record FailureData(int DocumentId, string Source, string? Sender, DateTime ReceivedAt, string? FailureReason);

        private sealed record TransactionData(
            int TransactionId, string Description, decimal Amount, string UserId, DateTime TransactionDate);

        private sealed record CategoryData(int CategoryId);
    }
}
