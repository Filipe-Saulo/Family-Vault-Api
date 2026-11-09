namespace FamilyVaultApi.Data.Entities
{
    // Uma linha por destino (e uma para o teto global de SMS), atualizada com UPDATE condicional
    public class VerificationSendLimit
    {
        public string Destination { get; set; } = string.Empty;
        public DateTime LastSentAt { get; set; }
        public DateTime DayWindowStartedAt { get; set; }
        public int DayCount { get; set; }
    }
}
