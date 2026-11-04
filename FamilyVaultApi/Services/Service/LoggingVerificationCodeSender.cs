using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    // Sem provedor de SMS: o código só aparece no log em Development
    public class LoggingVerificationCodeSender : IVerificationCodeSender
    {
        private readonly ILogger<LoggingVerificationCodeSender> _logger;
        private readonly IHostEnvironment _environment;

        public LoggingVerificationCodeSender(ILogger<LoggingVerificationCodeSender> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public Task SendAsync(string phoneE164, string code)
        {
            var masked = Mask(phoneE164);

            if (_environment.IsDevelopment())
                _logger.LogInformation("Código de recuperação para {Phone}: {Code}", masked, code);
            else
                _logger.LogInformation("Código de recuperação gerado para {Phone}.", masked);

            return Task.CompletedTask;
        }

        private static string Mask(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length <= 4)
                return "****";

            return new string('*', phone.Length - 4) + phone[^4..];
        }
    }
}
