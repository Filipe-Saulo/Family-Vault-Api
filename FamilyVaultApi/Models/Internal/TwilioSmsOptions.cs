namespace FamilyVaultApi.Models.Internal
{
    public class TwilioSmsOptions
    {
        public string AccountSid { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
    }
}
