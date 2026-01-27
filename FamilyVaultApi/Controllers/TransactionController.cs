using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/transaction")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService service)
        {
            _transactionService = service;
        }

        [HttpPost]
        [Authorize(Roles = "Administrator,User")]
        public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> Post([FromBody] CreateTransactionDto dto)
        {
            var result = await _transactionService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll),
                ApiResponse<TransactionResponseDto>.Created(result));
        }

        [HttpGet]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<PagedResult<TransactionResponseDto>>>> GetAll([FromQuery] TransactionQueryRequestDto query)
        {
            var result = await _transactionService.GetAllAsync(query);
            return Ok(ApiResponse<PagedResult<TransactionResponseDto>>.Ok(result));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _transactionService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Transação removida"));
        }
    }
}
