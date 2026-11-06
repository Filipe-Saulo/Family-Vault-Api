namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    // Corpo da rota de refresh: o access token vencido vem no header, e o web manda o refresh no cookie
    public class RefreshTokenBodyDto
    {
        public string? RefreshToken { get; init; }
    }
}
