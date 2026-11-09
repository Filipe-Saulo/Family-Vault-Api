using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IRegistrationCodeStore
    {
        // Gera um código novo de 6 dígitos para o destino, substituindo o anterior
        Task<string> IssueAsync(string destination, VerificationPurpose purpose);
        // Confere o código; cada erro conta, e o código é invalidado após o limite de erros
        Task<bool> VerifyAsync(string destination, VerificationPurpose purpose, string code);
        Task RemoveAsync(string destination, VerificationPurpose purpose);
    }
}
