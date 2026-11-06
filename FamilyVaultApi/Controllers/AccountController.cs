using FamilyVaultApi.Common;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [Route("api")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<string>>> Register([FromBody] CreateAccountRequestDto createAccountDto)
        {
            if (createAccountDto == null)
                return BadRequest(ApiResponse<string>.BadRequest(StandardMessages.NullBody));

            var result = await _accountService.RegisterAsync(createAccountDto);

            if (result.Any())
            {
                var mensagens = result.Select(e => $"{e.Code}: {e.Description}");
                return BadRequest(ApiResponse<string>.BadRequest(string.Join(" | ", mensagens)));
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse<string>.Created(null, "Usuário registrado com sucesso."));
        }

        [HttpPost("web/login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginWeb([FromBody] LoginRequestDto loginDto)
        {
            var authResponse = await _accountService.LoginAsync(loginDto, AuthClient.Web);
            SetRefreshTokenCookie(authResponse.RefreshToken);

            return Ok(ApiResponse<AuthResponseDto>.Ok(
                new AuthResponseDto
                {
                    UserId = authResponse.UserId,
                    Token = authResponse.Token,
                    RefreshToken = null
                },
                "Login realizado com sucesso."
            ));
        }

        [HttpPost("app/login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginApp([FromBody] LoginRequestDto loginDto)
        {
            var authResponse = await _accountService.LoginAsync(loginDto, AuthClient.App);
            return Ok(ApiResponse<AuthResponseDto>.Ok(authResponse, "Login realizado com sucesso."));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto? request = null)
        {
            // O app manda o refresh no corpo; o web, no cookie
            await _accountService.LogoutAsync(request?.RefreshToken ?? Request.Cookies["refreshToken"]);
            ClearRefreshTokenCookie();
            return Ok(ApiResponse<object>.Ok(null, "Logout realizado com sucesso."));
        }

        [HttpPost("web/refreshtoken")]
        public Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshTokenWeb([FromBody] RefreshTokenBodyDto? request)
            => RefreshTokenInternal(request, isWeb: true);

        [HttpPost("app/refreshtoken")]
        public Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshTokenApp([FromBody] RefreshTokenBodyDto? request)
            => RefreshTokenInternal(request, isWeb: false);

        private async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshTokenInternal(RefreshTokenBodyDto? request, bool isWeb)
        {
            var refreshToken = isWeb
                ? Request.Cookies["refreshToken"]
                : request?.RefreshToken;

            if (string.IsNullOrEmpty(refreshToken))
                throw new UnauthorizedAccessException("Refresh Token não encontrado.");

            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(accessToken) || accessToken == "null")
                throw new BadRequestException("Token inválido.");

            var result = await _accountService.RefreshTokenAsync(new RefreshTokenRequestDto
            {
                Token = accessToken,
                RefreshToken = refreshToken
            }, isWeb ? AuthClient.Web : AuthClient.App);

            var responseDto = new AuthResponseDto
            {
                Token = result.Token,
                UserId = result.UserId
            };

            if (isWeb)
                SetRefreshTokenCookie(result.RefreshToken);
            else
                responseDto.RefreshToken = result.RefreshToken;

            return Ok(ApiResponse<AuthResponseDto>.Ok(responseDto));
        }

        private void SetRefreshTokenCookie(string token)
        {
            DeleteLegacyRefreshTokenCookie();
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                // The web panel reaches the API through its own origin (Vite proxy in dev), so the
                // cookie is first-party: Lax is enough and Path keeps it off every other request.
                SameSite = SameSiteMode.Lax,
                Path = "/api",
                Expires = DateTimeOffset.UtcNow.AddDays(7),
            });
        }

        // Before the cookie moved to Path=/api it was issued with Path=/ and SameSite=None; a browser that
        // still holds that one would send two refreshToken cookies, so it is always removed.
        private void DeleteLegacyRefreshTokenCookie()
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
            });
        }

        private void ClearRefreshTokenCookie()
        {
            DeleteLegacyRefreshTokenCookie();
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/api",
            });
        }
    }
}
