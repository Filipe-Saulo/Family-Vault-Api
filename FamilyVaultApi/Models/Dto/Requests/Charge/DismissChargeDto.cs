namespace FamilyVaultApi.Models.Dto.Requests.Charge
{
    public class DismissChargeDto
    {
        // Ignorado até a leitura de e-mail existir (o upload não tem remetente)
        public bool IgnoreSender { get; set; }
    }
}
