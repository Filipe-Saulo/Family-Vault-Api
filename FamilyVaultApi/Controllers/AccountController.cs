using FamilyVaultApi.Common;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
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

            return Ok(ApiResponse<string>.Ok(null, "Usuário registrado com sucesso."));
        }

        [HttpPost("web/login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginWeb([FromBody] LoginRequestDto loginDto)
        {
            var authResponse = await _accountService.LoginAsync(loginDto);
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
            var authResponse = await _accountService.LoginAsync(loginDto);
            return Ok(ApiResponse<AuthResponseDto>.Ok(authResponse, "Login realizado com sucesso."));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto? request = null)
        {
            await _accountService.LogoutAsync(request?.Token);
            return Ok(ApiResponse<object>.Ok(null, "Logout realizado com sucesso."));
        }

        [HttpPost("web/refreshtoken")]
        public Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshTokenWeb([FromBody] RefreshTokenRequestDto? request)
            => RefreshTokenInternal(request, isWeb: true);

        [HttpPost("app/refreshtoken")]
        public Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshTokenApp([FromBody] RefreshTokenRequestDto? request)
            => RefreshTokenInternal(request, isWeb: false);

        private async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshTokenInternal(RefreshTokenRequestDto? request, bool isWeb)
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
            });

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

        [HttpPost("resetPassword")]
        public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] PasswordResetRequestDto passwordResetDto)
        {
            await _accountService.ResetPasswordAsync(passwordResetDto, User);
            return Ok(ApiResponse<object>.Ok(null, "Senha alterada com sucesso"));
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });
        }
    }
}
