using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Models.Dto.Responses.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using FamilyVaultApi.Models.Dto.Requests.User;

namespace FamilyVaultApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<PagedResult<UserResponseDto>>>> GetUsers([FromQuery] UserQueryRequestDto query)
        {
            var result = await _userService.GetUsersAsync(query);
            return Ok(ApiResponse<PagedResult<UserResponseDto>>.Ok(result));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUser([FromRoute] string id)
        {
            await _userService.DeleteUserAsync(id, User);
            return Ok(ApiResponse<object>.Ok("Usuário excluído com sucesso"));
        }
    }   
}
