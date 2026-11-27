using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Data
{
    // Configuração única do SQL Server, usada pela API e pela fábrica de design-time das migrations
    public static class SqlServerSetup
    {
        public static void Configure(DbContextOptionsBuilder options, string connectionString)
        {
            options.UseSqlServer(connectionString, sqlOptions =>
                // O Azure SQL serverless pausa sem uso e leva até ~1 min para acordar: as falhas dessa espera
                // (40613 e afins) estão na lista de transitórias do provedor, e as novas tentativas cobrem o intervalo
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null));
        }
    }
}
