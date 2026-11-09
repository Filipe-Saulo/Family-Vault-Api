namespace FamilyVaultApi.Models.Internal
{
    public class VerificationSendLimitOptions
    {
        public int MinIntervalSeconds { get; set; } = 60;
        public int PerDay { get; set; } = 5;
        public int GlobalSmsPerDay { get; set; } = 200;
    }
}
