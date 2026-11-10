using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    // Login folgado: cada teste usa um usuário novo, porque a conexão com o Gmail é uma por pessoa.
    // Initial limitada a 3 documentos para o teste do limite caber.
    public class GmailApiFixture : ApiFixture
    {
        public GmailApiFixture() : base(new Dictionary<string, string?>
        {
            ["IpRateLimiting:GeneralRules:2:Limit"] = "1000", // post:/api/app/login
            ["Gmail:InitialScanMaxDocuments"] = "3"
        })
        { }
    }

    // O worker fica desligado: a etapa 1 (GmailScanProcessor) e a etapa 2 (ChargeDocumentProcessor) são chamadas
    // à mão num scope. O Gmail é o FakeGmailApiClient e a IA, o FakeChargeExtractor.
    public class GmailTests : IClassFixture<GmailApiFixture>
    {
        private const int ExpenseCategoryId = 2;

        private readonly GmailApiFixture _api;

        public GmailTests(GmailApiFixture api) => _api = api;

        private FakeGmailApiClient Gmail => _api.Factory.GmailClient;
        private FakeChargeExtractor Extractor => _api.Factory.ChargeExtractor;

        // ---------- Conexão ----------

        [Fact]
        public async Task Connect_ShouldReturnAuthorizationUrlWithState()
        {
            var (_, user) = await _api.NewUserClientAsync();

            var response = await user.PostAsJsonAsync("/api/gmail/connect", new { });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var url = (await response.ReadEnvelopeAsync<ConnectData>()).Data!.AuthorizationUrl;
            StateFrom(url).Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Connect_ReturnUrlNotAllowed_ShouldReturnBadRequest()
        {
            var (_, user) = await _api.NewUserClientAsync();

            var response = await user.PostAsJsonAsync("/api/gmail/connect", new { returnUrl = "https://evil.example/x" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Connect_WithoutToken_ShouldReturnUnauthorized()
        {
            var response = await _api.AnonymousClient().PostAsJsonAsync("/api/gmail/connect", new { });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Callback_ValidState_ShouldConnectCreateInitialScanAndRedirectToReturnUrl()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();

            var location = await ConnectAsync(user, refreshToken, "Pessoa@Exemplo.com", FamilyVaultApiFactory.WebReturnUrl);

            location.Should().Be(FamilyVaultApiFactory.WebReturnUrl + "?gmail=connected");
            var status = await StatusAsync(user);
            status.Connected.Should().BeTrue();
            status.Status.Should().Be("Active");
            status.GoogleEmail.Should().Be("Pessoa@Exemplo.com");
            status.InitialScanCompletedAt.Should().BeNull();

            await using var db = Db(out var scope);
            using (scope)
            {
                var connection = await db.GmailConnections.AsNoTracking().SingleAsync(c => c.UserId == userId);
                connection.RefreshTokenEncrypted.Should().NotContain(refreshToken);
                var scan = await db.GmailScans.AsNoTracking().SingleAsync(s => s.UserId == userId);
                scan.Kind.Should().Be(GmailScanKind.Initial);
                scan.Status.Should().Be(GmailScanStatus.Running);
                (scan.WindowEnd - scan.WindowStart).TotalDays.Should().BeApproximately(90, 0.01);
            }
        }

        [Fact]
        public async Task Callback_WithoutReturnUrl_ShouldRedirectToFirstAllowedUrl()
        {
            var (_, user) = await _api.NewUserClientAsync();

            var location = await ConnectAsync(user, NewToken(), "a@exemplo.com", returnUrl: null);

            location.Should().Be(FamilyVaultApiFactory.AppReturnUrl + "?gmail=connected");
        }

        [Fact]
        public async Task Callback_StateUsedTwice_ShouldRedirectWithStateErrorTheSecondTime()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var state = await StartConnectAsync(user, FamilyVaultApiFactory.AppReturnUrl);
            var code = NewToken();
            Gmail.AddAuthorization(code, NewToken(), "a@exemplo.com");

            (await CallbackAsync($"code={code}&state={state}")).Should().EndWith("gmail=connected");
            var second = await CallbackAsync($"code={code}&state={state}");

            second.Should().Be(FamilyVaultApiFactory.AppReturnUrl + "?gmail=error&reason=state");
        }

        [Fact]
        public async Task Callback_ExpiredState_ShouldRedirectWithStateErrorWithoutConnecting()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var state = await StartConnectAsync(user, FamilyVaultApiFactory.AppReturnUrl);
            await using (var db = Db(out var scope))
            using (scope)
                await db.GmailOAuthStates.Where(s => s.UserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            var code = NewToken();
            Gmail.AddAuthorization(code, NewToken(), "a@exemplo.com");

            var location = await CallbackAsync($"code={code}&state={state}");

            location.Should().EndWith("gmail=error&reason=state");
            (await StatusAsync(user)).Connected.Should().BeFalse();
        }

        [Fact]
        public async Task Callback_UnknownState_ShouldRedirectToDefaultUrlWithStateError()
        {
            var location = await CallbackAsync("code=x&state=inexistente");

            location.Should().Be(FamilyVaultApiFactory.AppReturnUrl + "?gmail=error&reason=state");
        }

        [Fact]
        public async Task Callback_AccessDenied_ShouldRedirectWithDeniedError()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var state = await StartConnectAsync(user, FamilyVaultApiFactory.WebReturnUrl);

            var location = await CallbackAsync($"error=access_denied&state={state}");

            location.Should().Be(FamilyVaultApiFactory.WebReturnUrl + "?gmail=error&reason=denied");
            (await StatusAsync(user)).Connected.Should().BeFalse();
        }

        [Fact]
        public async Task Callback_WithoutGmailScope_ShouldRevokeAndRedirectWithScopeError()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var state = await StartConnectAsync(user, FamilyVaultApiFactory.AppReturnUrl);
            var code = NewToken();
            var refreshToken = NewToken();
            Gmail.AddAuthorization(code, refreshToken, "a@exemplo.com", hasGmailScope: false);

            var location = await CallbackAsync($"code={code}&state={state}");

            location.Should().EndWith("gmail=error&reason=scope");
            Gmail.WasRevoked(refreshToken).Should().BeTrue();
            (await StatusAsync(user)).Connected.Should().BeFalse();
        }

        [Fact]
        public async Task Status_WithoutConnection_ShouldReturnNotConnected()
        {
            var (_, user) = await _api.NewUserClientAsync();

            var status = await StatusAsync(user);

            status.Connected.Should().BeFalse();
            status.Status.Should().BeNull();
            status.GoogleEmail.Should().BeNull();
        }

        // ---------- Verificação inicial e etapa 2 ----------

        [Fact]
        public async Task InitialScan_ShouldCreateOneDocumentPerPdfAndProcessThemWithTheExtractor()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            var marker1 = NewMarker();
            var marker2 = NewMarker();
            Gmail.AddMessage(refreshToken, Message("m1", "cobranca@energia.test", -1, ("1", marker1), ("2", marker2)));
            Gmail.AddMessage(refreshToken, Message("m2", "nada@exemplo.test", -2));
            Extractor.Returns(marker1, Charge(marker1));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");

            await ProcessScansAsync();

            var scanId = await ScanIdAsync(userId);
            var scan = await GetScanAsync(user, scanId);
            scan.Kind.Should().Be("Initial");
            scan.Status.Should().Be("Completed");
            scan.Found.Should().Be(2);
            scan.Processed.Should().Be(0);
            var status = await StatusAsync(user);
            status.InitialScanCompletedAt.Should().NotBeNull();
            status.SyncedUntil.Should().NotBeNull();

            await ProcessDocumentsAsync();

            (await GetScanAsync(user, scanId)).Processed.Should().Be(2);
            Extractor.CallsFor(marker1).Should().Be(1);
            Extractor.CallsFor(marker2).Should().Be(1);
            var charges = await ChargesAsync(user);
            charges.Should().ContainSingle(c => c.Description == marker1 && c.Source == "Gmail");
            var documents = await DocumentsAsync(userId);
            documents.Select(d => d.ExternalId).Should().BeEquivalentTo("gmail:m1:1", "gmail:m1:2");
            documents.Should().OnlyContain(d => d.Sender == "cobranca@energia.test" && d.Status == InboundDocumentStatus.Done);
        }

        [Fact]
        public async Task InitialScan_ShouldStopAtTheMaximumOfDocumentsKeepingTheMostRecent()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            for (var i = 1; i <= 5; i++)
                Gmail.AddMessage(refreshToken, Message($"r{i}", "a@b.test", -i, ("1", NewMarker())));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");

            await ProcessScansAsync();

            (await DocumentsAsync(userId)).Select(d => d.ExternalId)
                .Should().BeEquivalentTo("gmail:r1:1", "gmail:r2:1", "gmail:r3:1");
        }

        [Fact]
        public async Task ManualScan_ShouldSkipAlreadyReadMessagesAndStartBeforeTheSavedPoint()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            Gmail.AddMessage(refreshToken, Message("old", "a@b.test", -3, ("1", NewMarker())));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");
            await ProcessScansAsync();
            var syncedUntil = (await StatusAsync(user)).SyncedUntil!.Value;
            Gmail.AddMessage(refreshToken, Message("new", "a@b.test", 0, ("1", NewMarker())));

            var response = await user.PostAsync("/api/gmail/scans", null);

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var scanId = (await response.ReadEnvelopeAsync<ScanCreatedData>()).Data!.ScanId;
            await ProcessScansAsync();
            var scan = await GetScanAsync(user, scanId);
            scan.Kind.Should().Be("Manual");
            scan.Status.Should().Be("Completed");
            scan.Found.Should().Be(1);
            (await DocumentsAsync(userId)).Select(d => d.ExternalId).Should().BeEquivalentTo("gmail:old:1", "gmail:new:1");
            await using var db = Db(out var serviceScope);
            using (serviceScope)
            {
                var entity = await db.GmailScans.AsNoTracking().SingleAsync(s => s.GmailScanId == scanId);
                entity.WindowStart.Should().BeCloseTo(syncedUntil.AddMinutes(-60), TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task StartScan_WithoutConnection_ShouldReturnBadRequest()
        {
            var (_, user) = await _api.NewUserClientAsync();

            (await user.PostAsync("/api/gmail/scans", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task StartScan_WithScanRunning_ShouldReturnConflict()
        {
            var (_, user) = await _api.NewUserClientAsync();
            await ConnectAsync(user, NewToken(), "a@exemplo.com"); // a Initial fica Running até o worker rodar

            (await user.PostAsync("/api/gmail/scans", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task GetScan_OfAnotherUser_ShouldReturnForbidden_AndUnknownShouldReturnNotFound()
        {
            var (ownerId, owner) = await _api.NewUserClientAsync();
            var (_, other) = await _api.NewUserClientAsync();
            await ConnectAsync(owner, NewToken(), "a@exemplo.com");
            var scanId = await ScanIdAsync(ownerId);

            (await other.GetAsync($"/api/gmail/scans/{scanId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await other.GetAsync("/api/gmail/scans/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task OversizePdf_ShouldBeCreatedAsFailedAndListedInFailures()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            Gmail.AddMessage(refreshToken, new FakeGmailApiClient.FakeMessage("big", "a@b.test", DateTime.UtcNow,
                new() { ("1", Pdf(NewMarker()), 11L * 1024 * 1024) }));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");

            await ProcessScansAsync();

            var failures = await user.GetFromJsonAsync<Envelope<PagedData<FailureData>>>("/api/charge/failures?PageSize=100");
            failures!.Data!.Items.Should().ContainSingle(f => f.Source == "Gmail" && f.FailureReason == "O PDF excede o tamanho máximo de 10 MB.");
            var scan = await GetScanAsync(user, await ScanIdAsync(userId));
            scan.Found.Should().Be(1);
            scan.Processed.Should().Be(1, because: "o documento já nasce terminado");
        }

        [Fact]
        public async Task Reconnect_WithAnotherGoogleAccount_ShouldDeleteThePendingDocumentsOfTheOldAccount()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var oldToken = NewToken();
            Gmail.AddMessage(oldToken, Message("o1", "a@b.test", -1, ("1", NewMarker())));
            await ConnectAsync(user, oldToken, "antiga@exemplo.com");
            await ProcessScansAsync();
            (await DocumentsAsync(userId)).Should().ContainSingle(d => d.Status == InboundDocumentStatus.Pending);

            await ConnectAsync(user, NewToken(), "nova@exemplo.com");

            (await DocumentsAsync(userId)).Should().BeEmpty();
            (await StatusAsync(user)).GoogleEmail.Should().Be("nova@exemplo.com");
        }

        [Fact]
        public async Task Reconnect_WithTheSameGoogleAccount_ShouldKeepThePendingDocuments()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var token = NewToken();
            Gmail.AddMessage(token, Message("s1", "a@b.test", -1, ("1", NewMarker())));
            await ConnectAsync(user, token, "mesma@exemplo.com");
            await ProcessScansAsync();

            await ConnectAsync(user, NewToken(), "Mesma@Exemplo.com");

            (await DocumentsAsync(userId)).Should().ContainSingle(d => d.Status == InboundDocumentStatus.Pending);
        }

        [Fact]
        public async Task EmailDeletedBeforeProcessing_ShouldFailWithReason()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            Gmail.AddMessage(refreshToken, Message("gone", "a@b.test", -1, ("1", NewMarker())));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");
            await ProcessScansAsync();
            Gmail.RemoveMessage(refreshToken, "gone");

            await ProcessDocumentsAsync();

            var document = (await DocumentsAsync(userId)).Single();
            document.Status.Should().Be(InboundDocumentStatus.Failed);
            document.FailureReason.Should().Be("O e-mail não está mais disponível no Gmail.");
        }

        // ---------- Acesso retirado, desconexão ----------

        [Fact]
        public async Task InvalidGrant_ShouldRevokeConnectionAndDocumentsShouldWaitForReconnection()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            var marker = NewMarker();
            Gmail.AddMessage(refreshToken, Message("w1", "a@b.test", -1, ("1", marker)));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");
            await ProcessScansAsync();
            Gmail.RevokeGrant(refreshToken);

            await ProcessDocumentsAsync();

            var status = await StatusAsync(user);
            status.Connected.Should().BeFalse();
            status.Status.Should().Be("Revoked");
            var document = (await DocumentsAsync(userId)).Single();
            document.Status.Should().Be(InboundDocumentStatus.Pending);
            document.Attempts.Should().Be(0);
            (await user.PostAsync("/api/gmail/scans", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // Reconectou: o documento volta a andar
            var newToken = NewToken();
            Gmail.AddMessage(newToken, Message("w1", "a@b.test", -1, ("1", marker)));
            await ConnectAsync(user, newToken, "a@exemplo.com");
            await ProcessScansAsync();
            await ProcessDocumentsAsync();

            (await DocumentsAsync(userId)).Single().Status.Should().Be(InboundDocumentStatus.Done);
        }

        [Fact]
        public async Task InvalidGrant_DuringScan_ShouldFailScanAndRevokeConnection()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            await ConnectAsync(user, refreshToken, "a@exemplo.com");
            Gmail.RevokeGrant(refreshToken);

            await ProcessScansAsync();

            (await GetScanAsync(user, await ScanIdAsync(userId))).Status.Should().Be("Failed");
            (await StatusAsync(user)).Status.Should().Be("Revoked");
        }

        [Fact]
        public async Task Disconnect_ShouldRevokeInGoogleDeletePendingGmailDocumentsAndKeepCharges()
        {
            var (userId, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            var processed = NewMarker();
            Gmail.AddMessage(refreshToken, Message("d1", "a@b.test", -1, ("1", processed)));
            Extractor.Returns(processed, Charge(processed));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");
            await ProcessScansAsync();
            await ProcessDocumentsAsync();
            Gmail.AddMessage(refreshToken, Message("d2", "a@b.test", 0, ("1", NewMarker())));
            (await user.PostAsync("/api/gmail/scans", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
            await ProcessScansAsync();

            var response = await user.DeleteAsync("/api/gmail/connection");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            Gmail.WasRevoked(refreshToken).Should().BeTrue();
            (await StatusAsync(user)).Connected.Should().BeFalse();
            (await DocumentsAsync(userId)).Select(d => d.ExternalId).Should().BeEquivalentTo("gmail:d1:1");
            (await ChargesAsync(user)).Should().ContainSingle(c => c.Description == processed);
        }

        [Fact]
        public async Task Disconnect_WithoutConnection_ShouldReturnNoContent()
        {
            var (_, user) = await _api.NewUserClientAsync();

            (await user.DeleteAsync("/api/gmail/connection")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // ---------- Remetentes ignorados ----------

        [Fact]
        public async Task DismissWithIgnoreSender_ShouldIgnoreTheSenderInTheNextSearch_AndDeleteShouldUndoIt()
        {
            var (_, user) = await _api.NewUserClientAsync();
            var refreshToken = NewToken();
            var marker = NewMarker();
            Gmail.AddMessage(refreshToken, Message("i1", "spam@loja.test", -1, ("1", marker)));
            Extractor.Returns(marker, Charge(marker));
            await ConnectAsync(user, refreshToken, "a@exemplo.com");
            await ProcessScansAsync();
            await ProcessDocumentsAsync();
            var charge = (await ChargesAsync(user)).Single(c => c.Description == marker);

            (await user.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/dismiss", new { ignoreSender = true }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var ignored = (await user.GetFromJsonAsync<Envelope<List<IgnoredData>>>("/api/gmail/ignored-senders"))!.Data!;
            ignored.Should().ContainSingle(s => s.Sender == "spam@loja.test");
            Gmail.AddMessage(refreshToken, Message("i2", "spam@loja.test", 0, ("1", NewMarker())));
            var scanId = (await (await user.PostAsync("/api/gmail/scans", null)).ReadEnvelopeAsync<ScanCreatedData>()).Data!.ScanId;
            await ProcessScansAsync();
            (await GetScanAsync(user, scanId)).Found.Should().Be(0);
            Gmail.Queries.Should().Contain(q => q.Contains("-from:spam@loja.test"));

            (await user.DeleteAsync($"/api/gmail/ignored-senders/{ignored[0].Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await user.GetFromJsonAsync<Envelope<List<IgnoredData>>>("/api/gmail/ignored-senders"))!.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteIgnoredSender_OfAnotherUser_ShouldReturnForbidden_AndUnknownShouldReturnNotFound()
        {
            var (ownerId, _) = await _api.NewUserClientAsync();
            var (_, other) = await _api.NewUserClientAsync();
            int id;
            await using (var db = Db(out var scope))
            using (scope)
            {
                var entity = new GmailIgnoredSender { UserId = ownerId, Sender = "x@y.test", CreatedAt = DateTime.UtcNow };
                db.GmailIgnoredSenders.Add(entity);
                await db.SaveChangesAsync();
                id = entity.GmailIgnoredSenderId;
            }

            (await other.DeleteAsync($"/api/gmail/ignored-senders/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await other.DeleteAsync("/api/gmail/ignored-senders/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // ---------- Helpers ----------

        private static string NewToken() => "t" + Guid.NewGuid().ToString("N");

        private static string NewMarker() => $"G{Guid.NewGuid():N}";

        private static byte[] Pdf(string marker) => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {marker}\n");

        private static FakeGmailApiClient.FakeMessage Message(string id, string sender, int daysAgo, params (string PartId, string Marker)[] pdfs) =>
            new(id, sender, DateTime.UtcNow.AddDays(daysAgo).AddMinutes(-1),
                pdfs.Select(p => (p.PartId, Pdf(p.Marker), (long)Pdf(p.Marker).Length)).ToList());

        private static ChargeExtractionResult Charge(string marker) => new()
        {
            IsCharge = true,
            Issuer = "Energia SA",
            Description = marker,
            Amount = 99.90m,
            DueDate = DateTime.UtcNow.Date.AddDays(10),
            SuggestedCategoryId = ExpenseCategoryId
        };

        private static string StateFrom(string authorizationUrl) =>
            QueryHelpers.ParseQuery(new Uri(authorizationUrl).Query)["state"].ToString();

        private static async Task<string> StartConnectAsync(HttpClient user, string? returnUrl)
        {
            var response = await user.PostAsJsonAsync("/api/gmail/connect", new { returnUrl });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return StateFrom((await response.ReadEnvelopeAsync<ConnectData>()).Data!.AuthorizationUrl);
        }

        // connect + callback com um code novo; devolve o Location do redirecionamento
        private async Task<string> ConnectAsync(HttpClient user, string refreshToken, string email, string? returnUrl = FamilyVaultApiFactory.AppReturnUrl)
        {
            var state = await StartConnectAsync(user, returnUrl);
            var code = NewToken();
            Gmail.AddAuthorization(code, refreshToken, email);
            return await CallbackAsync($"code={code}&state={Uri.EscapeDataString(state)}");
        }

        private async Task<string> CallbackAsync(string query)
        {
            var client = _api.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync($"/api/gmail/callback?{query}");
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return response.Headers.Location!.OriginalString;
        }

        private static async Task<StatusData> StatusAsync(HttpClient user)
        {
            var response = await user.GetAsync("/api/gmail/status");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<StatusData>()).Data!;
        }

        private static async Task<ScanData> GetScanAsync(HttpClient user, int scanId)
        {
            var response = await user.GetAsync($"/api/gmail/scans/{scanId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.ReadEnvelopeAsync<ScanData>()).Data!;
        }

        private static async Task<List<ChargeItem>> ChargesAsync(HttpClient user) =>
            (await user.GetFromJsonAsync<Envelope<PagedData<ChargeItem>>>("/api/charge?PageSize=100"))!.Data!.Items;

        private async Task ProcessScansAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<GmailScanProcessor>();
            var handled = 0;
            while (await processor.ProcessNextScanAsync(CancellationToken.None))
                (++handled).Should().BeLessThan(200, because: "as verificações devem esgotar");
        }

        private async Task ProcessDocumentsAsync()
        {
            using var scope = _api.Factory.Services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>();
            var processed = 0;
            while (await processor.ProcessNextAsync(CancellationToken.None))
                (++processed).Should().BeLessThan(500, because: "o processamento deve esgotar a fila");
        }

        private async Task<int> ScanIdAsync(string userId)
        {
            await using var db = Db(out var scope);
            using (scope)
                return await db.GmailScans.Where(s => s.UserId == userId).OrderBy(s => s.GmailScanId).Select(s => s.GmailScanId).FirstAsync();
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

        private sealed record PagedData<T>(int TotalCount, List<T> Items);
        private sealed record ConnectData(string AuthorizationUrl);
        private sealed record StatusData(bool Connected, string? GoogleEmail, string? Status, DateTime? InitialScanCompletedAt, DateTime? SyncedUntil);
        private sealed record ScanCreatedData(int ScanId);
        private sealed record ScanData(string Kind, string Status, int Found, int Processed);
        private sealed record IgnoredData(int Id, string Sender, DateTime CreatedAt);
        private sealed record FailureData(int DocumentId, string Source, string? Sender, string? FailureReason);
        private sealed record ChargeItem(int ChargeId, int DocumentId, string Source, string? Description, string Status);
    }
}
