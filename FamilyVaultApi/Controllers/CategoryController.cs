using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/category")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService service)
        {
            _categoryService = service;
        }

        [HttpPost]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<CategoryResponseDto>>> Post([FromBody] CreateCategoryDto dto)
        {
            var result = await _categoryService.CreateAsync(dto, User);
            return CreatedAtAction(nameof(GetAll),
                ApiResponse<CategoryResponseDto>.Created(result));
        }

        [HttpGet]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<PagedResult<CategoryResponseDto>>>> GetAll([FromQuery] CategoryQueryRequestDto query)
        {
            var result = await _categoryService.GetAllAsync(query, User);
            return Ok(ApiResponse<PagedResult<CategoryResponseDto>>.Ok(result));
        }

        [HttpGet("lookup")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<List<CategoryResponseDto>>>> GetLookup()
        {
            var result = await _categoryService.GetLookupAsync(User);
            return Ok(ApiResponse<List<CategoryResponseDto>>.Ok(result));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<CategoryResponseDto>>> Put(int id, [FromBody] UpdateCategoryDto dto)
        {
            var result = await _categoryService.UpdateAsync(id, dto, User);
            return Ok(ApiResponse<CategoryResponseDto>.Ok(result, "Categoria atualizada"));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _categoryService.DeleteAsync(id, User);
            return Ok(ApiResponse<object>.Ok(null, "Categoria removida"));
        }
    }
}
