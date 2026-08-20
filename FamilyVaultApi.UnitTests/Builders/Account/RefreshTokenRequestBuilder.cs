using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.UnitTests.Helpers;

namespace FamilyVaultApi.UnitTests.Builders.Account
{
    public class RefreshTokenRequestBuilder
    {
        private string _token = JwtTokenTestHelper.CreateToken();
        private string _refreshToken = Guid.NewGuid().ToString();

        public static RefreshTokenRequestBuilder New() => new();

        public RefreshTokenRequestBuilder WithToken(string token)
        {
            _token = token;
            return this;
        }

        public RefreshTokenRequestBuilder WithRefreshToken(string refreshToken)
        {
            _refreshToken = refreshToken;
            return this;
        }

        public RefreshTokenRequestDto Build() => new()
        {
            Token = _token,
            RefreshToken = _refreshToken
        };
    }
}
