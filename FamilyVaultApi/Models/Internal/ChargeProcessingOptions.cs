namespace FamilyVaultApi.Models.Internal
{
    // Seção "Charges" do appsettings
    public class ChargeProcessingOptions
    {
        public int MaxUploadBytes { get; set; } = 10485760;
        public ChargeDailyLimitOptions DailyLimits { get; set; } = new();
        public ChargeWorkerOptions Worker { get; set; } = new();
    }

    public class ChargeDailyLimitOptions
    {
        public int PerUser { get; set; } = 20;
        public int Total { get; set; } = 100;
    }

    public class ChargeWorkerOptions
    {
        public bool Enabled { get; set; } = true;
        public int PollIntervalMilliseconds { get; set; } = 10000;
        public int BatchSize { get; set; } = 5;
        public int MaxAttempts { get; set; } = 3;
        public int StuckAfterMinutes { get; set; } = 10;
        public int RetryDelaySeconds { get; set; } = 60;
    }
}
