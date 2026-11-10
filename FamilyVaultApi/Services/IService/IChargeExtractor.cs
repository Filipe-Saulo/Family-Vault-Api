using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Services.IService
{
    // Interpreta o documento e devolve só os campos da conta; não decide nada nem tem ferramenta
    public interface IChargeExtractor
    {
        Task<ChargeExtractionResult> ExtractAsync(ChargeExtractionInput input, CancellationToken cancellationToken);
    }
}
