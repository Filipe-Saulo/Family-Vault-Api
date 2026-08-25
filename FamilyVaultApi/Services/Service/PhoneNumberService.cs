using FamilyVaultApi.Services.IService;
using PhoneNumbers;

namespace FamilyVaultApi.Services.Service
{
    public class PhoneNumberService : IPhoneNumberService
    {
        private readonly PhoneNumberUtil _phoneNumberUtil = PhoneNumberUtil.GetInstance();

        public bool TryValidateAndFormat(string? rawPhone, string defaultRegion, out string? e164)
        {
            e164 = null;

            if (string.IsNullOrWhiteSpace(rawPhone))
                return false;

            try
            {
                var parsed = _phoneNumberUtil.Parse(rawPhone, defaultRegion);

                if (!_phoneNumberUtil.IsValidNumber(parsed))
                    return false;

                e164 = _phoneNumberUtil.Format(parsed, PhoneNumberFormat.E164);
                return true;
            }
            catch (NumberParseException)
            {
                return false;
            }
        }
    }
}
