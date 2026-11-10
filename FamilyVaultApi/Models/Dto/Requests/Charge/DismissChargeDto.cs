namespace FamilyVaultApi.Models.Dto.Requests.Charge
{
    public class DismissChargeDto
    {
        // Conta vinda do Gmail: o remetente entra em gmail_ignored_senders e sai das próximas buscas.
        // Sem efeito no upload, que não tem remetente.
        public bool IgnoreSender { get; set; }
    }
}
