namespace FamilyVaultApi.Exceptions
{
    // Falha do extrator com motivo mostrável ao usuário; não há nova tentativa
    public class ChargeExtractionException : ApplicationException
    {
        public ChargeExtractionException(string reason) : base(reason)
        {

        }
    }
}
