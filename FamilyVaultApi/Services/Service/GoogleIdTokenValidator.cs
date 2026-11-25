using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.Services.Service
{
    public class GoogleIdTokenValidator : IGoogleIdTokenValidator
    {
        private readonly GoogleSignInOptions _options;
        private readonly ILogger<GoogleIdTokenValidator> _logger;

        public GoogleIdTokenValidator(IOptions<GoogleSignInOptions> options, ILogger<GoogleIdTokenValidator> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<GoogleIdentity?> ValidateAsync(string idToken)
        {
            try
            {
                // A biblioteca busca e guarda em cache as chaves públicas do Google e confere assinatura,
                // iss (accounts.google.com ou https://accounts.google.com), exp e aud
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _options.ClientIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList()
                });

                return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.GivenName, payload.FamilyName, payload.Name);
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning("ID token do Google recusado: {Motivo}", ex.Message);
                return null;
            }
        }
    }
}
