namespace FamilyVaultApi.Services.IService
{
    public interface IPhoneNumberService
    {
        bool TryValidateAndFormat(string? rawPhone, string defaultRegion, out string? e164);
    }
}
