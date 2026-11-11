using FamilyVaultApi.Common;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace FamilyVaultApi.Mcp
{
    // Esquema só do /mcp: aceita o token pessoal fv_pat_... e nada mais (o JWT não vale ali). O principal leva só o
    // uid e o escopo, sem papel nem permissão, então os Services tratam o dono do token como um User comum, mesmo que
    // seja Administrator.
    public class McpTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "McpToken";
        public const string PolicyName = "Mcp";
        public const string ScopeClaim = "scope";
        public const string TokenIdClaim = "mcp_token_id";

        private readonly IMcpTokenService _tokenService;

        public McpTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IMcpTokenService tokenService)
            : base(options, logger, encoder)
        {
            _tokenService = tokenService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.NoResult();

            var rawToken = header["Bearer ".Length..].Trim();
            var token = await _tokenService.ValidateAsync(rawToken);
            if (token == null)
                return AuthenticateResult.Fail("Token MCP inválido, revogado ou vencido.");

            var identity = new ClaimsIdentity(
            [
                new Claim("uid", token.UserId),
                new Claim(ClaimTypes.NameIdentifier, token.UserId),
                new Claim(ScopeClaim, token.Scope),
                new Claim(TokenIdClaim, token.PersonalAccessTokenId.ToString())
            ], SchemeName);

            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return ApiResponseWriter.WriteAsync(Context, StatusCodes.Status401Unauthorized, "Acesso negado.");
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            return ApiResponseWriter.WriteAsync(Context, StatusCodes.Status403Forbidden, "Você não tem permissão para acessar este recurso.");
        }
    }
}
