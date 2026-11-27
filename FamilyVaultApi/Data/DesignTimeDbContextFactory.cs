using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FamilyVaultApi.Data
{
    // Usada pelo "dotnet ef" e pelo bundle das migrations no lugar do Program: assim aplicar migration não exige
    // JwtSettings:Key, Twilio nem Resend, só a connection string (user-secrets, ConnectionStrings__DefaultConnection
    // ou o --connection do bundle).
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        // Só para gerar migration sem banco configurado: nenhuma conexão é aberta com ela
        private const string PlaceholderConnection = "Server=localhost,1433;Database=FamilyVault;Integrated Security=false;TrustServerCertificate=True";

        public DatabaseContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var options = new DbContextOptionsBuilder<DatabaseContext>();
            SqlServerSetup.Configure(options, string.IsNullOrWhiteSpace(connectionString) ? PlaceholderConnection : connectionString);

            return new DatabaseContext(options.Options);
        }
    }
}
