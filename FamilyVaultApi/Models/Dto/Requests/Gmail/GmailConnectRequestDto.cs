namespace FamilyVaultApi.Models.Dto.Requests.Gmail
{
    public class GmailConnectRequestDto
    {
        // Para onde o callback volta; tem de estar em Gmail:AllowedReturnUrls. Vazio = o primeiro da lista.
        public string? ReturnUrl { get; set; }
    }
}
