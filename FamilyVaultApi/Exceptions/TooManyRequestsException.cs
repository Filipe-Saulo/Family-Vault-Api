namespace FamilyVaultApi.Exceptions
{
    public class TooManyRequestsException : ApplicationException
    {
        public TooManyRequestsException(string message) : base(message)
        {

        }
    }
}
