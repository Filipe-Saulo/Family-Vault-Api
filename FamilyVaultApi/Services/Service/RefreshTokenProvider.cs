using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.Services.Service
{
    public class RefreshTokenProviderOptions : DataProtectionTokenProviderOptions
    {
        public RefreshTokenProviderOptions()
        {
            Name = RefreshTokenProvider.ProviderName;
            TokenLifespan = TimeSpan.FromDays(7);
        }
    }

    // Provider exclusivo do refresh token: validade de 7 dias, sem afetar o provider padrão (reset de senha).
    public class RefreshTokenProvider : DataProtectorTokenProvider<User>
    {
        public const string ProviderName = "RefreshTokenProvider";

        public RefreshTokenProvider(
            IDataProtectionProvider dataProtectionProvider,
            IOptions<RefreshTokenProviderOptions> options,
            ILogger<DataProtectorTokenProvider<User>> logger)
            : base(dataProtectionProvider, options, logger)
        {
        }
    }
}
