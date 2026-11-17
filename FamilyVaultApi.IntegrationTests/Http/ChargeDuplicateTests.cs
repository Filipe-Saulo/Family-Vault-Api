using System.Net;
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
    // Possível conta duplicada (charges.possible_duplicate_charge_id): mesmo usuário, PendingReview, mesmo valor,
    // vencimento a até 10 dias e mesmo remetente (ou, sem remetente, mesmo emissor sem diferenciar maiúsculas).
    // Os documentos são gravados direto no banco (Gmail com remetente ou Upload sem) e o processador é chamado
    // direto, com o extrator falso. Cada teste usa um usuário novo e um valor próprio.
    public class ChargeDuplicateTests : IClassFixture<ApiFixture>
    {
        private const int ExpenseCategoryId = 2;

        private static int _amountSeed = 7000;

        private readonly ApiFixture _api;

        public ChargeDuplicateTests(ApiFixture api) => _api = api;

        private FakeChargeExtractor Extractor => _api.Factory.ChargeExtractor;

        [Fact]
        public async Task Process_GmailSameSenderAmountAndDueIn3Days_ShouldMarkBothWays()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due.AddDays(3));

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().Be(first.ChargeId);
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().Be(second.ChargeId);
        }

        [Fact]
        public async Task Process_DueIn11Days_ShouldNotMark()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due.AddDays(11));

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().BeNull();
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().BeNull();
        }

        [Fact]
        public async Task Process_DifferentSender_ShouldNotMark()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Gmail, "outro@agua.test", "Energia SA", amount, due.AddDays(1));

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().BeNull();
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().BeNull();
        }

        [Fact]
        public async Task Process_DifferentAmount_ShouldNotMark()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount + 0.01m, due);

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().BeNull();
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().BeNull();
        }

        [Theory]
        [InlineData(ChargeStatus.Confirmed)]
        [InlineData(ChargeStatus.Dismissed)]
        public async Task Process_CandidateAlreadyDecided_ShouldNotMark(ChargeStatus decided)
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            await SetStatusAsync(first.ChargeId, decided);
            var second = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due.AddDays(2));

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().BeNull();
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().BeNull();
        }

        [Fact]
        public async Task Process_UploadWithoutSenderAndSameIssuerDifferentCase_ShouldMarkBothWays()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Upload, null, "ENERGIA sa", amount, due.AddDays(4));

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().Be(first.ChargeId);
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().Be(second.ChargeId);
        }

        [Fact]
        public async Task Process_UploadWithoutSenderAndDifferentIssuer_ShouldNotMark()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Upload, null, "Agua SA", amount, due);

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().BeNull();
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().BeNull();
        }

        [Fact]
        public async Task Process_ThreeCharges_ShouldPointToClosestDueDateAndKeepExistingMark()
        {
            var (userId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(30);

            var a = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var b = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due.AddDays(6));
            // a já aponta para b; b aponta para a
            var c = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due.AddDays(5));

            // c vence a 1 dia de b e a 5 dias de a: aponta para b
            (await ReloadAsync(c)).PossibleDuplicateChargeId.Should().Be(b.ChargeId);
            // b já estava marcada (aponta para a): não é sobrescrita
            (await ReloadAsync(b)).PossibleDuplicateChargeId.Should().Be(a.ChargeId);
            (await ReloadAsync(a)).PossibleDuplicateChargeId.Should().Be(b.ChargeId);
        }

        [Fact]
        public async Task Process_AnotherUserWithSameData_ShouldNotMark()
        {
            var (userId, _) = await _api.NewUserAsync();
            var (otherId, _) = await _api.NewUserAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(otherId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);

            (await ReloadAsync(second)).PossibleDuplicateChargeId.Should().BeNull();
            (await ReloadAsync(first)).PossibleDuplicateChargeId.Should().BeNull();
        }

        [Fact]
        public async Task GetCharge_ShouldReturnPossibleDuplicateChargeId()
        {
            var (userId, client) = await _api.NewUserClientAsync();
            var amount = NewAmount();
            var due = DueIn(20);

            var first = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due);
            var second = await ProcessAsync(userId, InboundDocumentSource.Gmail, "cobranca@energia.test", "Energia SA", amount, due.AddDays(3));

            var detail = await client.GetAsync($"/api/charge/{second.ChargeId}");
            detail.StatusCode.Should().Be(HttpStatusCode.OK);
            (await detail.ReadEnvelopeAsync<ChargeView>()).Data!.PossibleDuplicateChargeId.Should().Be(first.ChargeId);

            var list = await client.GetAsync("/api/charge?PageSize=100");
            list.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await list.ReadEnvelopeAsync<PagedView>()).Data!.Items;
            items.Single(c => c.ChargeId == first.ChargeId).PossibleDuplicateChargeId.Should().Be(second.ChargeId);
        }

        // ---------- Helpers ----------

        private static decimal NewAmount() => Interlocked.Increment(ref _amountSeed) + 0.37m;

        private static DateTime DueIn(int days) => DateTime.UtcNow.Date.AddDays(days);

        // Grava o documento, define o que o extrator devolve, processa e devolve a charge criada
        private async Task<Charge> ProcessAsync(string userId, InboundDocumentSource source, string? sender,
            string issuer, decimal amount, DateTime due)
        {
            var marker = $"M{Guid.NewGuid():N}";
            Extractor.Returns(marker, new ChargeExtractionResult
            {
                IsCharge = true,
                Issuer = issuer,
                Description = marker,
                Amount = amount,
                DueDate = due,
                SuggestedCategoryId = ExpenseCategoryId
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

        private async Task<Charge> ReloadAsync(Charge charge)
        {
            using var scope = _api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            return await db.Charges.AsNoTracking().SingleAsync(c => c.ChargeId == charge.ChargeId);
        }

        private async Task SetStatusAsync(int chargeId, ChargeStatus status)
        {
            using var scope = _api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var entity = await db.Charges.SingleAsync(c => c.ChargeId == chargeId);
            entity.Status = status;
            await db.SaveChangesAsync();
        }

        private sealed record ChargeView(int ChargeId, int? PossibleDuplicateChargeId);

        private sealed record PagedView(int TotalCount, List<ChargeView> Items);
    }
}
