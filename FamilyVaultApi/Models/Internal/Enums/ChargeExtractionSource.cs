namespace FamilyVaultApi.Models.Internal.Enums
{
    // De onde saíram os dados da charge: do arquivo (PDF ou imagem), do corpo do e-mail (PDF protegido do Gmail
    // sem senha que abra) ou do valor informado pela pessoa
    public enum ChargeExtractionSource
    {
        Document,
        EmailBody,
        Manual
    }
}
