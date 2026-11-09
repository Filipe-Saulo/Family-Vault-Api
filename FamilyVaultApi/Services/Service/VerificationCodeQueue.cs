using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using System.Threading.Channels;

namespace FamilyVaultApi.Services.Service
{
    public record VerificationCodeMessage(VerificationChannel Channel, string Destination, string Code, VerificationPurpose Purpose);

    // É o IVerificationCodeSender que os services recebem: só enfileira, para o tempo de resposta
    // do /password/forgot e do /register/code não revelar se a conta existe. Quem entrega é o VerificationCodeDispatcher.
    public class VerificationCodeQueue : IVerificationCodeSender
    {
        public const string SmsDeliveryKey = "verification-code-sms";
        public const string EmailDeliveryKey = "verification-code-email";
        private const int Capacity = 100;

        private readonly Channel<VerificationCodeMessage> _channel = Channel.CreateBounded<VerificationCodeMessage>(
            new BoundedChannelOptions(Capacity) { SingleReader = true });
        private readonly ILogger<VerificationCodeQueue> _logger;

        public VerificationCodeQueue(ILogger<VerificationCodeQueue> logger)
        {
            _logger = logger;
        }

        public ChannelReader<VerificationCodeMessage> Reader => _channel.Reader;

        public Task SendAsync(VerificationCodeMessage message)
        {
            if (!_channel.Writer.TryWrite(message))
                _logger.LogWarning("Fila de códigos cheia: envio para {Destination} descartado.", DestinationMask.Mask(message.Destination));

            return Task.CompletedTask;
        }
    }
}
