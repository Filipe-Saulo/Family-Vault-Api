using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/transactiontype")]
    public class TransactionTypeController : ControllerBase
    {
        private readonly ITransactionTypeService _transactionTypeService;

        public TransactionTypeController(ITransactionTypeService service)
        {
            _transactionTypeService = service;
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<TransactionTypeResponseDto>>> Post([FromBody] CreateTransactionTypeDto dto)
        {
            var result = await _transactionTypeService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll),
                ApiResponse<TransactionTypeResponseDto>.Created(result));
        }

        [HttpGet]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<List<TransactionTypeResponseDto>>>> GetAll([FromQuery] bool? isActive)
        {
            var result = await _transactionTypeService.GetAllAsync(isActive);
            return Ok(ApiResponse<List<TransactionTypeResponseDto>>.Ok(result));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<TransactionTypeResponseDto>>> Put(int id, [FromBody] UpdateTransactionTypeDto dto)
        {
            var result = await _transactionTypeService.UpdateAsync(id, dto);
            return Ok(ApiResponse<TransactionTypeResponseDto>.Ok(result, "TransactionType atualizado"));
        }
    }
}
