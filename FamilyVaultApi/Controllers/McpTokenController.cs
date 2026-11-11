using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.McpToken;
using FamilyVaultApi.Models.Dto.Responses.McpToken;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    // Tokens pessoais do servidor MCP. Gerenciados com o JWT de sempre; o token criado só vale em /mcp.
    [ApiController]
    [Route("api/user/me/mcp-tokens")]
    [Authorize(Roles = "Administrator,User")]
    public class McpTokenController : ControllerBase
    {
        private readonly IMcpTokenService _service;

        public McpTokenController(IMcpTokenService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<McpTokenResponseDto>>>> GetMine()
        {
            var result = await _service.GetMineAsync(User);
            return Ok(ApiResponse<List<McpTokenResponseDto>>.Ok(result));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<McpTokenCreatedResponseDto>>> Create([FromBody] CreateMcpTokenDto dto)
        {
            var result = await _service.CreateAsync(dto, User);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<McpTokenCreatedResponseDto>.Created(result));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Revoke([FromRoute] int id)
        {
            await _service.RevokeAsync(id, User);
            return NoContent();
        }
    }
}
