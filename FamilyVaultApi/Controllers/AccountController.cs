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

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginRequestDto loginDto)
        {
            var authResponse = await _accountService.LoginAsync(loginDto);
            
            if (!string.IsNullOrEmpty(loginDto.Email))
            {
                Response.Cookies.Append(
                "refreshToken",
                authResponse.RefreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                }
                );


                return Ok(ApiResponse<AuthResponseDto>.Ok(
                new AuthResponseDto
                {
                    UserId = authResponse.UserId,
                    Token = authResponse.Token,
                    RefreshToken = null
                },
                "Administrador logado com sucesso."
                ));
            }            
            return Ok(ApiResponse<AuthResponseDto>.Ok(authResponse, "Usuário logado com sucesso."));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto? request = null)
        {
            await _accountService.LogoutAsync(request?.Token);
            return Ok(ApiResponse<object>.Ok(null, "Logout realizado com sucesso."));
        }

        [HttpPost("refreshtoken")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken([FromBody] RefreshTokenRequestDto? request)
        {
            string? refreshToken;
            bool isWeb = Request.Headers["User-Agent"].ToString().Contains("Mozilla");

            // Obtém o refresh token — cookie no web, body no app
            refreshToken = isWeb
                ? Request.Cookies["refreshToken"]
                : request?.RefreshToken;

            if (string.IsNullOrEmpty(refreshToken))
                throw new UnauthorizedAccessException("Refresh Token não encontrado.");

            var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(accessToken) || accessToken == "null")
                throw new BadRequestException("Token inválido.");

            // Revalida tokens
            var result = await _accountService.RefreshTokenAsync(
                new RefreshTokenRequestDto
                {
                    Token = accessToken,
                    RefreshToken = refreshToken
                },
                isWeb
            );

            var responseDto = new AuthResponseDto
            {
                Token = result.Token,
                UserId = result.UserId
            };

            if (isWeb)
            {
                // Web → grava cookie
                Response.Cookies.Append(
                    "refreshToken",
                    result.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,                        
                    }
                );
            }
            else
            {
                // Mobile → retorna no body
                responseDto.RefreshToken = result.RefreshToken;
            }

            return Ok(ApiResponse<AuthResponseDto>.Ok(responseDto));
        }
    
        [HttpPost("resetPassword")]
        public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] PasswordResetRequestDto passwordResetDto)
        {
            await _accountService.ResetPasswordAsync(passwordResetDto, User);
            return Ok(ApiResponse<object>.Ok(null, "Senha alterada com sucesso"));
        }
    }
}
