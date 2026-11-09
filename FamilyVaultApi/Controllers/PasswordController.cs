using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [Route("api/password")]
    [ApiController]
    public class PasswordController : ControllerBase
    {
        private readonly IPasswordService _passwordService;

        public PasswordController(IPasswordService passwordService)
        {
            _passwordService = passwordService;
        }

        [HttpPost("forgot")]
        public async Task<ActionResult<ApiResponse<object>>> Forgot([FromBody] ForgotPasswordRequestDto dto)
        {
            await _passwordService.ForgotPasswordAsync(dto);
            return StatusCode(StatusCodes.Status202Accepted,
                ApiResponse<object>.Accepted("Se o telefone ou e-mail estiver cadastrado, enviaremos um código."));
        }

        [HttpPost("reset")]
        public async Task<ActionResult<ApiResponse<object>>> Reset([FromBody] ResetPasswordWithCodeRequestDto dto)
        {
            await _passwordService.ResetPasswordAsync(dto);
            return Ok(ApiResponse<object>.Ok(null, "Senha alterada com sucesso."));
        }

        [HttpPost("change")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<object>>> Change([FromBody] ChangePasswordRequestDto dto)
        {
            await _passwordService.ChangePasswordAsync(dto, User);
            return Ok(ApiResponse<object>.Ok(null, "Senha alterada com sucesso. Faça login novamente."));
        }
    }
}
