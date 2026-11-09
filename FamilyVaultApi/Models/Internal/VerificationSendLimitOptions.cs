namespace FamilyVaultApi.Models.Internal
{
    public class VerificationSendLimitOptions
    {
        public int PerMinute { get; set; } = 1;
        public int PerDay { get; set; } = 5;
    }
}
