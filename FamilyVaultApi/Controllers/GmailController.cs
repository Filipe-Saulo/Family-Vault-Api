using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.Gmail;
using FamilyVaultApi.Models.Dto.Responses.Gmail;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/gmail")]
    [Authorize(Roles = "Administrator,User")]
    public class GmailController : ControllerBase
    {
        private readonly IGmailConnectionService _gmailService;

        public GmailController(IGmailConnectionService service)
        {
            _gmailService = service;
        }

        [HttpPost("connect")]
        public async Task<ActionResult<ApiResponse<GmailConnectResponseDto>>> Connect([FromBody] GmailConnectRequestDto? dto = null)
        {
            var result = await _gmailService.ConnectAsync(dto, User);
            return Ok(ApiResponse<GmailConnectResponseDto>.Ok(result));
        }

        // O Google redireciona o navegador para cá: sem JWT, validado pelo state de uso único
        [AllowAnonymous]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
        {
            var returnUrl = await _gmailService.HandleCallbackAsync(code, state, error, cancellationToken);
            return Redirect(returnUrl);
        }

        [HttpGet("status")]
        public async Task<ActionResult<ApiResponse<GmailStatusResponseDto>>> Status()
        {
            var result = await _gmailService.GetStatusAsync(User);
            return Ok(ApiResponse<GmailStatusResponseDto>.Ok(result));
        }

        [HttpDelete("connection")]
        public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
        {
            await _gmailService.DisconnectAsync(User, cancellationToken);
            return NoContent();
        }

        [HttpPost("scans")]
        public async Task<ActionResult<ApiResponse<GmailScanCreatedResponseDto>>> StartScan()
        {
            var result = await _gmailService.StartScanAsync(User);
            return Accepted(new ApiResponse<GmailScanCreatedResponseDto>(StandardMessages.Get(HttpStatusCode.Accepted), result));
        }

        [HttpGet("scans/{id:int}")]
        public async Task<ActionResult<ApiResponse<GmailScanResponseDto>>> GetScan(int id)
        {
            var result = await _gmailService.GetScanAsync(id, User);
            return Ok(ApiResponse<GmailScanResponseDto>.Ok(result));
        }

        [HttpGet("ignored-senders")]
        public async Task<ActionResult<ApiResponse<List<GmailIgnoredSenderResponseDto>>>> ListIgnoredSenders()
        {
            var result = await _gmailService.ListIgnoredSendersAsync(User);
            return Ok(ApiResponse<List<GmailIgnoredSenderResponseDto>>.Ok(result));
        }

        [HttpDelete("ignored-senders/{id:int}")]
        public async Task<IActionResult> DeleteIgnoredSender(int id)
        {
            await _gmailService.DeleteIgnoredSenderAsync(id, User);
            return NoContent();
        }
    }
}
