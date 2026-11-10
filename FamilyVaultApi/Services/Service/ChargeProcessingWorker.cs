using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.Services.Service
{
    // Lê do banco a cada tick (sobrevive a reinício). Etapa 1: as verificações do Gmail (só com o Gmail configurado).
    // Etapa 2: os documentos (só com IChargeExtractor registrado).
    public class ChargeProcessingWorker : BackgroundService
    {
        // Config zerada ou negativa derrubaria o host no PeriodicTimer
        private const int MinPollIntervalMilliseconds = 100;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ChargeProcessingOptions _options;
        private readonly ILogger<ChargeProcessingWorker> _logger;

        public ChargeProcessingWorker(IServiceScopeFactory scopeFactory, IOptions<ChargeProcessingOptions> options, ILogger<ChargeProcessingWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Worker.Enabled)
                return;

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(_options.Worker.PollIntervalMilliseconds, MinPollIntervalMilliseconds)));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var scanProcessor = scope.ServiceProvider.GetService<GmailScanProcessor>();
                    if (scanProcessor != null)
                        await scanProcessor.ProcessNextScanAsync(stoppingToken);

                    if (scope.ServiceProvider.GetService<IChargeExtractor>() == null)
                        continue;

                    var processor = scope.ServiceProvider.GetRequiredService<ChargeDocumentProcessor>();

                    await processor.RequeueStuckAsync();

                    for (var i = 0; i < _options.Worker.BatchSize; i++)
                    {
                        if (!await processor.ProcessNextAsync(stoppingToken))
                            break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Falha ao processar os documentos de contas.");
                }
            }
        }
    }
}
