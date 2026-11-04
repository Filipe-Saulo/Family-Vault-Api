namespace FamilyVaultApi.Exceptions
{
    public class NotFoundException : ApplicationException
    {
        public NotFoundException(string name, object key) : base($"Registro não encontrado: {name} (id {key}).")
        {

        }
    }
}
