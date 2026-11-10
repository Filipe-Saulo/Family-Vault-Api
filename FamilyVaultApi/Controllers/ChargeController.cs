using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FamilyVaultApi.Exceptions;
using System.Net;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/charge")]
    [Authorize(Roles = "Administrator,User")]
    public class ChargeController : ControllerBase
    {
        // Teto do corpo um pouco acima do limite do arquivo (Charges:MaxUploadBytes, 10 MB): o Kestrel corta antes de
        // ler um corpo enorme, e o arquivo acima do limite é recusado antes de ir para a memória.
        private const long MaxRequestBodyBytes = 11 * 1024 * 1024;

        private readonly IChargeService _chargeService;
        private readonly int _maxUploadBytes;

        public ChargeController(IChargeService service, IOptions<ChargeProcessingOptions> options)
        {
            _chargeService = service;
            _maxUploadBytes = options.Value.MaxUploadBytes;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(MaxRequestBodyBytes)]
        public async Task<ActionResult<ApiResponse<ChargeUploadResponseDto>>> Upload(IFormFile? file)
        {
            byte[] content = [];
            if (file != null)
            {
                if (file.Length > _maxUploadBytes)
                    throw new BadRequestException($"O arquivo excede o tamanho máximo de {_maxUploadBytes / (1024 * 1024)} MB.");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                content = stream.ToArray();
            }

            var result = await _chargeService.UploadAsync(content, User);
            return Accepted(new ApiResponse<ChargeUploadResponseDto>(StandardMessages.Get(HttpStatusCode.Accepted), result));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<ChargeResponseDto>>>> GetAll([FromQuery] ChargeQueryRequestDto query)
        {
            var result = await _chargeService.GetAllAsync(query, User);
            return Ok(ApiResponse<PagedResult<ChargeResponseDto>>.Ok(result));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ChargeResponseDto>>> GetById(int id)
        {
            var result = await _chargeService.GetByIdAsync(id, User);
            return Ok(ApiResponse<ChargeResponseDto>.Ok(result));
        }

        [HttpGet("failures")]
        public async Task<ActionResult<ApiResponse<PagedResult<ChargeFailureResponseDto>>>> GetFailures([FromQuery] PagedFilterDto query)
        {
            var result = await _chargeService.GetFailuresAsync(query, User);
            return Ok(ApiResponse<PagedResult<ChargeFailureResponseDto>>.Ok(result));
        }

        [HttpPost("{id:int}/confirm")]
        public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> Confirm(int id, [FromBody] ConfirmChargeDto dto)
        {
            var result = await _chargeService.ConfirmAsync(id, dto, User);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<TransactionResponseDto>.Created(result));
        }

        [HttpPost("{id:int}/dismiss")]
        public async Task<IActionResult> Dismiss(int id, [FromBody] DismissChargeDto? dto = null)
        {
            await _chargeService.DismissAsync(id, dto, User);
            return NoContent();
        }
    }
}
