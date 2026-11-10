using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FamilyVaultApi.IntegrationTests.Http;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FamilyVaultApi.IntegrationTests
{
    // Sobe a API no ambiente "Testing" com SQLite em memória no lugar do MySQL.
    // O schema (com os papéis do HasData) é criado antes da subida, porque o Program
    // roda o AdminSeeder logo depois do Build.
    public class FamilyVaultApiFactory : WebApplicationFactory<Program>
    {
        private const string JwtKey = "integration-tests-jwt-key-with-32-plus-bytes";
        // Chave fixa só dos testes (32 bytes em base64); a real fica em user-secrets ou Gmail__TokenEncryptionKey
        public const string GmailTokenKey = "dGVzdGVzLWRlLWludGVncmFjYW8tY2hhdmUtMzJieXQ=";
        public const string AppReturnUrl = "familyvault://gmail";
        public const string WebReturnUrl = "http://localhost:5173/gmail";

        private readonly SqliteConnection _connection;
        private readonly string? _adminEmail;
        private readonly string? _adminPassword;
        private readonly IDictionary<string, string?>? _settings;

        // Extrator falso das contas por upload: o teste configura o resultado por marcador nos bytes
        public FakeChargeExtractor ChargeExtractor { get; } = new();

        // Gmail falso: o teste monta a caixa de e-mail e as autorizações
        public FakeGmailApiClient GmailClient { get; } = new();

        public FamilyVaultApiFactory(string? adminEmail, string? adminPassword, Action<DatabaseContext>? seedBeforeStartup = null,
            IDictionary<string, string?>? settings = null)
        {
            _settings = settings;
            _adminEmail = adminEmail;
            _adminPassword = adminPassword;

            // O banco em memória vive enquanto a conexão estiver aberta
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = new DatabaseContext(BuildOptions());
            context.Database.EnsureCreated();
            seedBeforeStartup?.Invoke(context);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("JwtSettings:Key", JwtKey);
            builder.UseSetting("AdminSeed:Email", _adminEmail ?? string.Empty);
            builder.UseSetting("AdminSeed:Password", _adminPassword ?? string.Empty);

            // O worker concorreria com os requests na conexão única do SQLite; os testes processam à mão.
            // Limites do dia folgados por padrão (usuários de teste são reaproveitados); o teste do limite sobrescreve.
            builder.UseSetting("Charges:Worker:Enabled", "false");
            builder.UseSetting("Charges:Worker:RetryDelaySeconds", "0");
            builder.UseSetting("Charges:DailyLimits:PerUser", "1000");
            builder.UseSetting("Charges:DailyLimits:Total", "100000");
            // Gmail ligado com credenciais de mentira; o cliente do Google é trocado pelo falso abaixo
            builder.UseSetting("Gmail:ClientId", "test-client-id");
            builder.UseSetting("Gmail:ClientSecret", "test-client-secret");
            builder.UseSetting("Gmail:TokenEncryptionKey", GmailTokenKey);
            builder.UseSetting("Gmail:RedirectUri", "https://localhost/api/gmail/callback");
            builder.UseSetting("Gmail:AllowedReturnUrls:0", AppReturnUrl);
            builder.UseSetting("Gmail:AllowedReturnUrls:1", WebReturnUrl);
            builder.UseSetting("Gmail:ManualScanMinIntervalSeconds", "0");
            foreach (var (key, value) in _settings ?? new Dictionary<string, string?>())
                builder.UseSetting(key, value);

            builder.ConfigureServices(services =>
            {
                services.AddDbContext<DatabaseContext>(options => options.UseSqlite(_connection));
                services.RemoveAll<IChargeExtractor>();
                services.AddSingleton<IChargeExtractor>(ChargeExtractor);
                services.RemoveAll<IGmailApiClient>();
                services.AddSingleton<IGmailApiClient>(GmailClient);
            });
        }

        public async Task<List<User>> GetAdministratorsAsync()
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            return (await userManager.GetUsersInRoleAsync("Administrator")).ToList();
        }

        public async Task<bool> CheckPasswordAsync(User user, string password)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            return await userManager.CheckPasswordAsync(user, password);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _connection.Dispose();
        }

        private DbContextOptions<DatabaseContext> BuildOptions() =>
            new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(_connection).Options;
    }
}
