using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyVaultApi.IntegrationTests
{
    // Sobe a API no ambiente "Testing" com SQLite em memória no lugar do MySQL.
    // O schema (com os papéis do HasData) é criado antes da subida, porque o Program
    // roda o AdminSeeder logo depois do Build.
    public class FamilyVaultApiFactory : WebApplicationFactory<Program>
    {
        private const string JwtKey = "integration-tests-jwt-key-with-32-plus-bytes";

        private readonly SqliteConnection _connection;
        private readonly string? _adminEmail;
        private readonly string? _adminPassword;

        public FamilyVaultApiFactory(string? adminEmail, string? adminPassword, Action<DatabaseContext>? seedBeforeStartup = null)
        {
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

            builder.ConfigureServices(services =>
                services.AddDbContext<DatabaseContext>(options => options.UseSqlite(_connection)));
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
