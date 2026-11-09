namespace FamilyVaultApi.Services.IService
{
    public interface IRegistrationCodeStore
    {
        // Gera um código novo de 6 dígitos para o destino, substituindo o anterior
        string Issue(string destination);
        // Confere o código; cada erro conta, e o código é invalidado após o limite de erros
        bool Verify(string destination, string code);
        void Remove(string destination);
    }
}
