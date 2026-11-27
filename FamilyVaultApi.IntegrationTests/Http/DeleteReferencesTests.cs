using System.Net;
using System.Net.Http.Json;
using System.Text;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // Regressão: no SQL Server as FKs de charges/inbound_documents não têm SET NULL/CASCADE no banco
    // (ClientSetNull/ClientCascade); os repositórios zeram as referências antes de apagar. Aqui o SQLite aplica as FKs.
    public class DeleteReferencesTests : IClassFixture<ApiFixture>
    {
        private const int ExpenseCategoryId = 2;
        private const int ExpensePurposeId = 1;

        private static int _amountSeed = 9000;

        private readonly ApiFixture _api;

        public DeleteReferencesTests(ApiFixture api) => _api = api;

        private FakeChargeExtractor Extractor => _api.Factory.ChargeExtractor;

        [Fact]
        public async Task DeleteTransaction_CreatedFromConfirmedCharge_ShouldKeepChargeWithNullTransactionId()
        {
            var (userId, client) = await _api.UserClientAsync(0);
            var charge = await ProcessAsync(userId, InboundDocumentSource.Upload, null, NewAmount(), DueIn(20), ExpenseCategoryId);

            var confirm = await client.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", new
            {
                description = "Conta a apagar",
                amount = 150.25m,
                dueDate = DueIn(10),
                categoryId = ExpenseCategoryId
            });
            confirm.StatusCode.Should().Be(HttpStatusCode.Created);
            var transactionId = (await confirm.ReadEnvelopeAsync<IdData>()).Data!.TransactionId;
            (await ReloadAsync(charge.ChargeId))!.TransactionId.Should().Be(transactionId);

            var delete = await client.DeleteAsync($"/api/transaction/{transactionId}");

            ((int)delete.StatusCode).Should().BeInRange(200, 299);
            var after = await ReloadAsync(charge.ChargeId);
            after.Should().NotBeNull();
            after!.TransactionId.Should().BeNull();
            after.Status.Should().Be(ChargeStatus.Confirmed);
        }

        [Fact]
        public async Task DeleteTransaction_PointedByChargeAsPossibleDuplicate_ShouldClearReference()
        {
            var (userId, client) = await _api.UserClientAsync(0);
            var transactionId = await ConfirmNewChargeAsync(userId, client);
            var pending = await ProcessAsync(userId, InboundDocumentSource.Upload, null, NewAmount(), DueIn(25), ExpenseCategoryId);
            await UpdateChargeAsync(pending.ChargeId, c => c.PossibleDuplicateTransactionId = transactionId);

            var delete = await client.DeleteAsync($"/api/transaction/{transactionId}");

            ((int)delete.StatusCode).Should().BeInRange(200, 299);
            (await ReloadAsync(pending.ChargeId))!.PossibleDuplicateTransactionId.Should().BeNull();
        }

        [Fact]
        public async Task DeleteCategory_SuggestedByCharge_ShouldKeepChargeWithNullSuggestion()
        {
            var (userId, client) = await _api.UserClientAsync(0);
            var created = await client.PostAsJsonAsync("/api/category",
                new { description = $"Sug-{Guid.NewGuid():N}"[..30], categoryPurposeId = ExpensePurposeId });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var categoryId = (await created.ReadEnvelopeAsync<CategoryData>()).Data!.CategoryId;

            var charge = await ProcessAsync(userId, InboundDocumentSource.Upload, null, NewAmount(), DueIn(20), categoryId);
            charge.SuggestedCategoryId.Should().Be(categoryId);

            var delete = await client.DeleteAsync($"/api/category/{categoryId}");

            ((int)delete.StatusCode).Should().BeInRange(200, 299);
            var after = await ReloadAsync(charge.ChargeId);
            after.Should().NotBeNull();
            after!.SuggestedCategoryId.Should().BeNull();
        }

        [Fact]
        public async Task DeleteUser_WithChargesDuplicatesAndGmailScan_ShouldRemoveRowsAndClearOtherUsersReferences()
        {
            var (victimId, _) = await _api.NewUserAsync();
            var (otherId, _) = await _api.NewUserAsync();
            var admin = await _api.AdminClientAsync();

            // categoria e transação (de uma cobrança confirmada) do usuário que será apagado
            var victimClient = await ClientForAsync(victimId);
            var amount = NewAmount();
            var due = DueIn(20);
            var categoryId = await CreateCategoryAsync(victimClient);

            var first = await ProcessAsync(victimId, InboundDocumentSource.Gmail, "cobranca@energia.test", amount, due, categoryId);
            var second = await ProcessAsync(victimId, InboundDocumentSource.Gmail, "cobranca@energia.test", amount, due.AddDays(3), categoryId);
            (await ReloadAsync(second.ChargeId))!.PossibleDuplicateChargeId.Should().Be(first.ChargeId);

            var confirm = await victimClient.PostAsJsonAsync($"/api/charge/{first.ChargeId}/confirm", new
            {
                description = "Conta do usuario",
                amount = 99.90m,
                dueDate = DueIn(10),
                categoryId = ExpenseCategoryId
            });
            confirm.StatusCode.Should().Be(HttpStatusCode.Created);
            var transactionId = (await confirm.ReadEnvelopeAsync<IdData>()).Data!.TransactionId;

            // varredura do Gmail ligada aos documentos do usuário
            int scanId;
            using (var scope = _api.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var scan = new GmailScan
                {
                    UserId = victimId,
                    Kind = GmailScanKind.Manual,
                    WindowStart = DateTime.UtcNow.AddDays(-30),
                    WindowEnd = DateTime.UtcNow,
                    Status = GmailScanStatus.Completed,
                    Found = 2,
                    Processed = 2,
                    CreatedAt = DateTime.UtcNow
                };
                db.GmailScans.Add(scan);
                await db.SaveChangesAsync();
                scanId = scan.GmailScanId;
                await db.InboundDocuments
                    .Where(d => d.InboundDocumentId == first.InboundDocumentId || d.InboundDocumentId == second.InboundDocumentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.GmailScanId, scanId));
            }

            // cobrança de outro usuário apontando para linhas do usuário apagado (referências cruzadas)
            var foreign = await ProcessAsync(otherId, InboundDocumentSource.Upload, null, NewAmount(), DueIn(30), ExpenseCategoryId);
            await UpdateChargeAsync(foreign.ChargeId, c =>
            {
                c.TransactionId = transactionId;
                c.PossibleDuplicateTransactionId = transactionId;
                c.SuggestedCategoryId = categoryId;
                c.PossibleDuplicateChargeId = second.ChargeId;
            });

            var delete = await admin.DeleteAsync($"/api/user/{victimId}");

            ((int)delete.StatusCode).Should().BeInRange(200, 299);
            using var read = _api.Factory.Services.CreateScope();
            var readDb = read.ServiceProvider.GetRequiredService<DatabaseContext>();
            (await readDb.Users.AnyAsync(u => u.Id == victimId)).Should().BeFalse();
            (await readDb.Charges.AnyAsync(c => c.ChargeId == first.ChargeId || c.ChargeId == second.ChargeId)).Should().BeFalse();
            (await readDb.InboundDocuments.AnyAsync(d => d.UserId == victimId)).Should().BeFalse();
            (await readDb.GmailScans.AnyAsync(s => s.GmailScanId == scanId)).Should().BeFalse();
            (await readDb.Transactions.AnyAsync(t => t.TransactionId == transactionId)).Should().BeFalse();
            (await readDb.Categories.AnyAsync(c => c.CategoryId == categoryId)).Should().BeFalse();

            var kept = await readDb.Charges.AsNoTracking().SingleAsync(c => c.ChargeId == foreign.ChargeId);
            kept.TransactionId.Should().BeNull();
            kept.PossibleDuplicateTransactionId.Should().BeNull();
            kept.SuggestedCategoryId.Should().BeNull();
            kept.PossibleDuplicateChargeId.Should().BeNull();
        }

        // ---------- Helpers ----------

        private static decimal NewAmount() => Interlocked.Increment(ref _amountSeed) + 0.41m;

        private static DateTime DueIn(int days) => DateTime.UtcNow.Date.AddDays(days);

        // Usuário sem login prévio: entra com o telefone
        private async Task<HttpClient> ClientForAsync(string userId)
        {
            string phone;
            using (var scope = _api.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                phone = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).PhoneNumber!;
            }

            var login = await _api.AnonymousClient().PostAsJsonAsync("/api/app/login", new { phone, password = ApiFixture.UserPassword });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = (await login.ReadEnvelopeAsync<AuthData>()).Data!;
            var client = _api.AnonymousClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
            return client;
        }

        private static async Task<int> CreateCategoryAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync("/api/category",
                new { description = $"Cat-{Guid.NewGuid():N}"[..30], categoryPurposeId = ExpensePurposeId });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.ReadEnvelopeAsync<CategoryData>()).Data!.CategoryId;
        }

        private async Task<int> ConfirmNewChargeAsync(string userId, HttpClient client)
        {
            var charge = await ProcessAsync(userId, InboundDocumentSource.Upload, null, NewAmount(), DueIn(20), ExpenseCategoryId);
            var confirm = await client.PostAsJsonAsync($"/api/charge/{charge.ChargeId}/confirm", new
            {
                description = "Conta confirmada",
                amount = 150.25m,
                dueDate = DueIn(10),
                categoryId = ExpenseCategoryId
            });
            confirm.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await confirm.ReadEnvelopeAsync<IdData>()).Data!.TransactionId;
        }

        private async Task<Charge> ProcessAsync(string userId, InboundDocumentSource source, string? sender,
            decimal amount, DateTime due, int categoryId)
        {
            var marker = $"M{Guid.NewGuid():N}";
            Extractor.Returns(marker, new ChargeExtractionResult
            {
                IsCharge = true,
                Issuer = "Energia SA",
                Description = marker,
                Amount = amount,
                DueDate = due,
                SuggestedCategoryId = categoryId
            });

            int documentId;
            using (var scope = _api.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var document = new InboundDocument
                {
                    UserId = userId,
                    Source = source,
                    ExternalId = (source == InboundDocumentSource.Gmail ? "gmail:" : "upload:") + marker,
                    Sender = sender,
                    ReceivedAt = DateTime.UtcNow,
                    Status = InboundDocumentStatus.Pending,
                    TemporaryContent = Encoding.ASCII.GetBytes($"%PDF-1.4\n% {marker}\n"),
                    ContentType = "application/pdf",
                    CreatedAt = DateTime.UtcNow
                };
                db.InboundDocuments.Add(document);
                await db.SaveChangesAsync();
                documentId = document.InboundDocumentId;
            }

            using (var scope = _api.Factory.Services.CreateScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>();
                var processed = 0;
                while (await processor.ProcessNextAsync(CancellationToken.None))
                    (++processed).Should().BeLessThan(500, because: "o processamento deve esgotar a fila");
            }

            using var read = _api.Factory.Services.CreateScope();
            var readDb = read.ServiceProvider.GetRequiredService<DatabaseContext>();
            return await readDb.Charges.AsNoTracking().SingleAsync(c => c.InboundDocumentId == documentId);
        }

        private async Task<Charge?> ReloadAsync(int chargeId)
        {
            using var scope = _api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            return await db.Charges.AsNoTracking().SingleOrDefaultAsync(c => c.ChargeId == chargeId);
        }

        private async Task UpdateChargeAsync(int chargeId, Action<Charge> change)
        {
            using var scope = _api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var entity = await db.Charges.SingleAsync(c => c.ChargeId == chargeId);
            change(entity);
            await db.SaveChangesAsync();
        }

        private sealed record IdData(int TransactionId);

        private sealed record CategoryData(int CategoryId);

        private sealed record AuthData(string UserId, string Token);
    }
}
