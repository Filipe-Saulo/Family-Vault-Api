using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    public class VerificationCodeDispatcher : BackgroundService
    {
        private readonly VerificationCodeQueue _queue;
        private readonly IVerificationCodeSender _smsDelivery;
        private readonly IVerificationCodeSender _emailDelivery;
        private readonly ILogger<VerificationCodeDispatcher> _logger;

        public VerificationCodeDispatcher(
            VerificationCodeQueue queue,
            [FromKeyedServices(VerificationCodeQueue.SmsDeliveryKey)] IVerificationCodeSender smsDelivery,
            [FromKeyedServices(VerificationCodeQueue.EmailDeliveryKey)] IVerificationCodeSender emailDelivery,
            ILogger<VerificationCodeDispatcher> logger)
        {
            _queue = queue;
            _smsDelivery = smsDelivery;
            _emailDelivery = emailDelivery;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var delivery = message.Channel == VerificationChannel.Email ? _emailDelivery : _smsDelivery;
                    await delivery.SendAsync(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao enviar o código de verificação ({Purpose}) para {Destination}.", message.Purpose, DestinationMask.Mask(message.Destination));
                }
            }
        }
    }
}
