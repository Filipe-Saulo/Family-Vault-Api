using FamilyVaultApi.Common;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    // Usado em Development e Testing, no lugar da Twilio e do Resend (uma instância por canal): o código só aparece no log em Development
    public class LoggingVerificationCodeSender : IVerificationCodeSender
    {
        private readonly ILogger<LoggingVerificationCodeSender> _logger;
        private readonly IHostEnvironment _environment;

        public LoggingVerificationCodeSender(ILogger<LoggingVerificationCodeSender> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public Task SendAsync(VerificationCodeMessage message)
        {
            var masked = DestinationMask.Mask(message.Destination);

            if (_environment.IsDevelopment())
                _logger.LogInformation("Código de verificação ({Purpose}, {Channel}) para {Destination}: {Code}", message.Purpose, message.Channel, masked, message.Code);
            else
                _logger.LogInformation("Código de verificação ({Purpose}, {Channel}) gerado para {Destination}.", message.Purpose, message.Channel, masked);

            return Task.CompletedTask;
        }
    }
}
