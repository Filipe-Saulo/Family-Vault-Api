using FamilyVaultApi.Repositories.Repository;

namespace FamilyVaultApi.Services.Service
{
    public class VerificationDataCleanupService : BackgroundService
    {
        private static readonly TimeSpan Period = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VerificationDataCleanupService> _logger;

        public VerificationDataCleanupService(IServiceScopeFactory scopeFactory, ILogger<VerificationDataCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Period);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<VerificationDataCleaner>().DeleteExpiredAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Falha ao limpar os dados vencidos de verificação.");
                }
            }
        }
    }
}
