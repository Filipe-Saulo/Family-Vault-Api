using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // PDF com senha pela API: o documento fica PasswordRequired e a pessoa informa a senha ou o valor. PDFs reais
    // criptografados (PDFsharp); o worker fica desligado e o processamento é chamado à mão. Usa a fixture do Gmail
    // (login folgado), com um usuário novo por teste.
    public class ChargePasswordTests : IClassFixture<GmailApiFixture>
    {
        private const int ExpenseCategoryId = 2;
        private const string Password = "12345";
        private const string Reason = "O PDF está protegido por senha.";

        private readonly GmailApiFixture _api;

        public ChargePasswordTests(GmailApiFixture api) => _api = api;

        private FakeChargeExtractor Extractor => _api.Factory.ChargeExtractor;
        private FakeGmailApiClient Gmail => _api.Factory.GmailClient;

        // ---------- Upload ----------

        [Fact]
        public async Task Upload_PdfWithPassword_ShouldWaitForPasswordAndAppearInFailures()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var marker = NewMarker();

            var documentId = await UploadDocumentAsync(user, TestPdfs.Create(marker, userPassword: Password));
            await ProcessDocumentsAsync();

            var document = await GetDocumentAsync(documentId);
            document.Status.Should().Be(InboundDocumentStatus.PasswordRequired);
            document.TemporaryContent.Should().NotBeNull("o upload guarda o arquivo enquanto espera a senha");
            Extractor.CallsFor(marker).Should().Be(0);

            var failure = (await FailuresAsync(user)).Single(f => f.DocumentId == documentId);
            failure.Status.Should().Be("PasswordRequired");
            failure.FailureReason.Should().Be(Reason);
            failure.PasswordAttemptsRemaining.Should().Be(5);
        }

        [Fact]
        public async Task SubmitPassword_WrongThenRight_ShouldCountAttemptAndThenExtractTheUnlockedPdf()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var marker = NewMarker();
            Extractor.Returns(marker, Charge(marker));
            var documentId = await UploadDocumentAsync(user, TestPdfs.Create(marker, userPassword: Password));
            await ProcessDocumentsAsync();

            var wrong = await SubmitPasswordAsync(user, documentId, "00000");
            wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await wrong.ReadEnvelopeAsync<string>()).Message.Should().Be("Senha incorreta. Restam 4 tentativa(s).");
            (await FailuresAsync(user)).Single(f => f.DocumentId == documentId).PasswordAttemptsRemaining.Should().Be(4);

            var right = await SubmitPasswordAsync(user, documentId, Password);
            right.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await right.ReadEnvelopeAsync<UploadData>()).Data!.DocumentId.Should().Be(documentId);

            await ProcessDocumentsAsync();

            var charge = (await ChargesAsync(user)).Single(c => c.DocumentId == documentId);
            charge.Status.Should().Be("PendingReview");
            charge.Description.Should().Be(marker);
            var document = await GetDocumentAsync(documentId);
            document.Status.Should().Be(InboundDocumentStatus.Done);
            document.TemporaryContent.Should().BeNull("o PDF aberto é apagado quando a extração termina");
            (await FailuresAsync(user)).Should().NotContain(f => f.DocumentId == documentId);
        }

        [Fact]
        public async Task SubmitPassword_AfterTheLimit_ShouldReturnTooManyRequestsButStillAllowInformingTheValue()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(user);

            for (var i = 0; i < 5; i++)
                (await SubmitPasswordAsync(user, documentId, "00000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var blocked = await SubmitPasswordAsync(user, documentId, Password);
            blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await blocked.ReadEnvelopeAsync<string>()).TraceId.Should().NotBeNullOrWhiteSpace();
            (await FailuresAsync(user)).Single(f => f.DocumentId == documentId).PasswordAttemptsRemaining.Should().Be(0);

            (await ManualAsync(user, documentId)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task SubmitPassword_DocumentOfAnotherUser_ShouldReturnForbidden()
        {
            var (_, owner) = await _api.NewUserClientAsync();
            var (_, other) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(owner);

            (await SubmitPasswordAsync(other, documentId, Password)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await ManualAsync(other, documentId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await GetDocumentAsync(documentId)).PasswordAttempts.Should().Be(0);
        }

        [Fact]
        public async Task SubmitPassword_UnknownDocument_ShouldReturnNotFound()
        {
            var (_, user) = await _api.NewUserClientAsync();

            (await SubmitPasswordAsync(user, int.MaxValue, Password)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SubmitPassword_EmptyPassword_ShouldReturnBadRequestWithoutSpendingAttempt()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(user);

            (await SubmitPasswordAsync(user, documentId, "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetDocumentAsync(documentId)).PasswordAttempts.Should().Be(0);
        }

        [Fact]
        public async Task Upload_PdfWithOwnerPasswordOnly_ShouldBeExtractedWithoutAsking()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var marker = NewMarker();
            Extractor.Returns(marker, Charge(marker));

            var documentId = await UploadDocumentAsync(user, TestPdfs.Create(marker, ownerPassword: "dono"));
            await ProcessDocumentsAsync();

            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.Done);
            (await ChargesAsync(user)).Should().ContainSingle(c => c.DocumentId == documentId);
        }

        // ---------- Informar o valor ----------

        [Fact]
        public async Task Manual_ShouldCreateExpenseOnDueDateAndConfirmedCharge()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(user);
            var due = DateTime.UtcNow.Date.AddDays(8);

            var response = await ManualAsync(user, documentId, due, issuer: "Banco X");

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var transaction = (await response.ReadEnvelopeAsync<TransactionData>()).Data!;
            transaction.UserId.Should().Be(userId);
            transaction.Amount.Should().Be(512.30m);
            transaction.TransactionDate.Date.Should().Be(due);

            var charge = (await ChargesAsync(user)).Single(c => c.DocumentId == documentId);
            charge.Status.Should().Be("Confirmed");
            charge.TransactionId.Should().Be(transaction.TransactionId);
            charge.Issuer.Should().Be("Banco X");
            charge.Amount.Should().Be(512.30m);

            var document = await GetDocumentAsync(documentId);
            document.Status.Should().Be(InboundDocumentStatus.Done);
            document.TemporaryContent.Should().BeNull();
            (await FailuresAsync(user)).Should().NotContain(f => f.DocumentId == documentId);
        }

        [Fact]
        public async Task Manual_Twice_ShouldReturnConflictAndKeepSingleTransaction()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(user);

            (await ManualAsync(user, documentId)).StatusCode.Should().Be(HttpStatusCode.Created);
            var again = await ManualAsync(user, documentId);

            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await TransactionCountAsync(user)).Should().Be(1);
            (await SubmitPasswordAsync(user, documentId, Password)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Manual_InvalidBody_ShouldReturnBadRequestAndKeepWaiting()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(user);

            var response = await user.PostAsJsonAsync($"/api/charge/documents/{documentId}/manual",
                new { description = "", amount = 0, categoryId = ExpenseCategoryId });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.PasswordRequired);
        }

        // ---------- Prazo do upload ----------

        [Fact]
        public async Task Expire_UploadWaitingLongerThanRetention_ShouldFailAndDropTheFile()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var documentId = await NewPasswordRequiredUploadAsync(user);
            await using (var db = Db(out var scope))
            using (scope)
            {
                await db.InboundDocuments.Where(d => d.InboundDocumentId == documentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.PasswordRequiredAt, DateTime.UtcNow.AddDays(-8)));
            }

            using (var scope = _api.Factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>().ExpirePasswordRequiredAsync();

            var document = await GetDocumentAsync(documentId);
            document.Status.Should().Be(InboundDocumentStatus.Failed);
            document.FailureReason.Should().Be("O prazo para informar a senha terminou.");
            document.TemporaryContent.Should().BeNull();
            var failure = (await FailuresAsync(user)).Single(f => f.DocumentId == documentId);
            failure.Status.Should().Be("Failed");
            failure.PasswordAttemptsRemaining.Should().BeNull();
        }

        // ---------- Gmail e senha lembrada ----------

        [Fact]
        public async Task Gmail_PasswordRememberedForSender_ShouldOpenTheNextInvoiceWithoutAsking()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            const string sender = "faturas@banco.test";
            var first = NewMarker();
            var second = NewMarker();
            Extractor.Returns(first, Charge(first));
            Extractor.Returns(second, Charge(second));
            Gmail.AddMessage(refreshToken, new FakeGmailApiClient.FakeMessage("m1-" + first, sender, DateTime.UtcNow.AddDays(-2),
                [("2", TestPdfs.Create(first, userPassword: Password), 1000)]));

            await ConnectAsync(user, refreshToken, "pessoa@exemplo.test");
            await ProcessScansAsync();
            await ProcessDocumentsAsync();

            var waiting = (await DocumentsAsync(userId)).Single();
            waiting.Status.Should().Be(InboundDocumentStatus.PasswordRequired);
            waiting.TemporaryContent.Should().BeNull("o PDF do Gmail não é guardado; é baixado de novo com a senha");
            var scan = await GetScanAsync(user, waiting.GmailScanId!.Value);
            scan.Processed.Should().Be(scan.Found, "o documento à espera da senha conta como tratado na verificação");

            (await SubmitPasswordAsync(user, waiting.InboundDocumentId, Password, remember: true)).StatusCode.Should().Be(HttpStatusCode.Accepted);
            await ProcessDocumentsAsync();
            (await ChargesAsync(user)).Should().ContainSingle(c => c.Description == first);

            await using (var db = Db(out var scope))
            using (scope)
            {
                var saved = await db.PdfSenderPasswords.SingleAsync(p => p.UserId == userId);
                saved.Sender.Should().Be(sender);
                saved.PasswordEncrypted.Should().NotContain(Password);
            }

            // Fatura nova do mesmo remetente: abre com a senha lembrada
            Gmail.AddMessage(refreshToken, new FakeGmailApiClient.FakeMessage("m2-" + second, sender, DateTime.UtcNow.AddMinutes(-1),
                [("2", TestPdfs.Create(second, userPassword: Password), 1000)]));
            (await user.PostAsync("/api/gmail/scans", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
            await ProcessScansAsync();
            await ProcessDocumentsAsync();

            (await ChargesAsync(user)).Should().ContainSingle(c => c.Description == second);
            (await DocumentsAsync(userId)).Should().OnlyContain(d => d.Status == InboundDocumentStatus.Done);
        }

        // ---------- Helpers ----------

        private static string NewMarker() => $"P{Guid.NewGuid():N}";

        private static string NewToken() => "t" + Guid.NewGuid().ToString("N");

        private static ChargeExtractionResult Charge(string marker) => new()
        {
            IsCharge = true,
            Issuer = "Banco SA",
            Description = marker,
            Amount = 210.00m,
            DueDate = DateTime.UtcNow.Date.AddDays(10),
            SuggestedCategoryId = ExpenseCategoryId
        };

        private async Task<int> NewPasswordRequiredUploadAsync(HttpClient user)
        {
            var documentId = await UploadDocumentAsync(user, TestPdfs.Create(NewMarker(), userPassword: Password));
            await ProcessDocumentsAsync();
            (await GetDocumentAsync(documentId)).Status.Should().Be(InboundDocumentStatus.PasswordRequired);
            return documentId;
        }

        private static async Task<int> UploadDocumentAsync(HttpClient client, byte[] bytes)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "file", "fatura.pdf");
            var response = await client.PostAsync("/api/charge/upload", content);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            return (await response.ReadEnvelopeAsync<UploadData>()).Data!.DocumentId;
        }

        private static Task<HttpResponseMessage> SubmitPasswordAsync(HttpClient client, int documentId, string password, bool remember = false) =>
            client.PostAsJsonAsync($"/api/charge/documents/{documentId}/password", new { password, rememberForSender = remember });

        private static Task<HttpResponseMessage> ManualAsync(HttpClient client, int documentId, DateTime? due = null, string? issuer = null) =>
            client.PostAsJsonAsync($"/api/charge/documents/{documentId}/manual", new
            {
                description = "Fatura do cartão",
                amount = 512.30m,
                dueDate = due ?? DateTime.UtcNow.Date.AddDays(8),
                categoryId = ExpenseCategoryId,
                issuer
            });

        private static async Task<List<FailureData>> FailuresAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/charge/failures?PageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<PagedData<FailureData>>()).Data!.Items;
        }

        private static async Task<List<ChargeData>> ChargesAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/charge?PageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<PagedData<ChargeData>>()).Data!.Items;
        }

        private static async Task<int> TransactionCountAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/transaction?PageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<PagedData<TransactionData>>()).Data!.TotalCount;
        }

        private async Task ProcessDocumentsAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>();
            var processed = 0;
            while (await processor.ProcessNextAsync(CancellationToken.None))
                (++processed).Should().BeLessThan(500, because: "o processamento deve esgotar a fila");
        }

        private async Task ProcessScansAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<GmailScanProcessor>();
            var handled = 0;
            while (await processor.ProcessNextScanAsync(CancellationToken.None))
                (++handled).Should().BeLessThan(200, because: "as verificações devem esgotar");
        }

        // connect + callback com um code novo
        private async Task ConnectAsync(HttpClient user, string refreshToken, string email)
        {
            var connect = await user.PostAsJsonAsync("/api/gmail/connect", new { });
            connect.StatusCode.Should().Be(HttpStatusCode.OK);
            var url = (await connect.ReadEnvelopeAsync<ConnectData>()).Data!.AuthorizationUrl;
            var state = QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();
            var code = NewToken();
            Gmail.AddAuthorization(code, refreshToken, email);

            var client = _api.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var callback = await client.GetAsync($"/api/gmail/callback?code={code}&state={Uri.EscapeDataString(state)}");
            callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            callback.Headers.Location!.OriginalString.Should().Contain("gmail=connected");
        }

        private static async Task<ScanData> GetScanAsync(HttpClient user, int scanId)
        {
            var response = await user.GetAsync($"/api/gmail/scans/{scanId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<ScanData>()).Data!;
        }

        private async Task<InboundDocument> GetDocumentAsync(int documentId)
        {
            await using var db = Db(out var scope);
            using (scope)
                return await db.InboundDocuments.AsNoTracking().SingleAsync(d => d.InboundDocumentId == documentId);
        }

        private async Task<List<InboundDocument>> DocumentsAsync(string userId)
        {
            await using var db = Db(out var scope);
            using (scope)
                return await db.InboundDocuments.AsNoTracking().Where(d => d.UserId == userId).ToListAsync();
        }

        private DatabaseContext Db(out IServiceScope scope)
        {
            scope = _api.Factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        }

        private sealed record UploadData(int DocumentId);
        private sealed record ConnectData(string AuthorizationUrl);
        private sealed record ScanData(string Kind, string Status, int Found, int Processed);
        private sealed record PagedData<T>(int TotalCount, List<T> Items);

        private sealed record FailureData(int DocumentId, string Source, string? Sender, DateTime ReceivedAt, string? FailureReason,
            string Status, int? PasswordAttemptsRemaining);

        private sealed record ChargeData(int ChargeId, int DocumentId, string? Issuer, string? Description, decimal? Amount,
            string Status, int? TransactionId);

        private sealed record TransactionData(int TransactionId, decimal Amount, string UserId, DateTime TransactionDate);
    }
}
