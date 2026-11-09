namespace FamilyVaultApi.Services.IService
{
    public interface IVerificationSendLimiter
    {
        // Registra o envio e devolve true; devolve false, sem registrar, se o destino (telefone ou e-mail) já atingiu o limite
        bool TryAcquire(string destination);
    }
}
