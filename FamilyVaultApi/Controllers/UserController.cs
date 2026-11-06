using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Models.Dto.Responses.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FamilyVaultApi.Models.Dto.Requests.User;

namespace FamilyVaultApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IPasswordService _passwordService;

        public UserController(IUserService userService, IPasswordService passwordService)
        {
            _userService = userService;
            _passwordService = passwordService;
        }

        [HttpGet]
        [Authorize(Policy = nameof(PermissionCode.ManageUsers))]
        public async Task<ActionResult<ApiResponse<PagedResult<UserResponseDto>>>> GetUsers([FromQuery] UserQueryRequestDto query)
        {
            var result = await _userService.GetUsersAsync(query);
            return Ok(ApiResponse<PagedResult<UserResponseDto>>.Ok(result));
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> CreateUser([FromBody] CreateUserRequestDto dto)
        {
            var result = await _userService.CreateUserAsync(dto);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<UserResponseDto>.Created(result));
        }

        [HttpGet("me")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetMe()
        {
            var result = await _userService.GetMeAsync(User);
            return Ok(ApiResponse<UserResponseDto>.Ok(result));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateUser([FromRoute] string id, [FromBody] UpdateUserDto dto)
        {
            var result = await _userService.UpdateUserAsync(id, dto, User);
            return Ok(ApiResponse<UserResponseDto>.Ok(result, "Usuário atualizado com sucesso"));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser([FromRoute] string id)
        {
            await _userService.DeleteUserAsync(id, User);
            return Ok(ApiResponse<object>.Ok(null, "Usuário excluído com sucesso"));
        }

        [HttpPut("{id}/role")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> ChangeRole([FromRoute] string id, [FromBody] ChangeUserRoleDto dto)
        {
            var result = await _userService.ChangeRoleAsync(id, dto, User);
            return Ok(ApiResponse<UserResponseDto>.Ok(result, "Papel alterado com sucesso"));
        }

        [HttpPost("{id}/password")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<object>>> SetPassword([FromRoute] string id, [FromBody] SetUserPasswordRequestDto dto)
        {
            await _passwordService.SetUserPasswordAsync(id, dto);
            return Ok(ApiResponse<object>.Ok(null, "Senha do usuário alterada com sucesso"));
        }

        [HttpGet("{userId}/permissions")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<List<PermissionCode>>>> GetPermissions([FromRoute] string userId)
        {
            var result = await _userService.GetPermissionsAsync(userId);
            return Ok(ApiResponse<List<PermissionCode>>.Ok(result));
        }

        [HttpPost("{userId}/permissions")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<object>>> GrantPermission([FromRoute] string userId, [FromBody] GrantPermissionDto dto)
        {
            await _userService.GrantPermissionAsync(userId, dto.Permission);
            return Ok(ApiResponse<object>.Ok(null, "Permissão concedida"));
        }

        [HttpDelete("{userId}/permissions/{permission}")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<object>>> RevokePermission([FromRoute] string userId, [FromRoute] PermissionCode permission)
        {
            await _userService.RevokePermissionAsync(userId, permission);
            return Ok(ApiResponse<object>.Ok(null, "Permissão revogada"));
        }
    }
}
