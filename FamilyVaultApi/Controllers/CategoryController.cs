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
            var result = await _categoryService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll),
                ApiResponse<CategoryResponseDto>.Created(result));
        }

        [HttpGet]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<PagedResult<CategoryResponseDto>>>> GetAll([FromQuery] CategoryQueryRequestDto query)
        {
            var result = await _categoryService.GetAllAsync(query);
            return Ok(ApiResponse<PagedResult<CategoryResponseDto>>.Ok(result));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = nameof(PermissionCode.ManageCategories))]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            await _categoryService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Categoria removida"));
        }
    }
}
