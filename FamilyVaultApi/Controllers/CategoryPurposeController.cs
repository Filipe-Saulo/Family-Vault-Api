using FamilyVaultApi.Common;
using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FamilyVaultApi.Controllers
{
    [ApiController]
    [Route("api/categorypurpose")]
    public class CategoryPurposeController : ControllerBase
    {
        private readonly ICategoryPurposeService _categoryPurposeService;

        public CategoryPurposeController(ICategoryPurposeService service)
        {
            _categoryPurposeService = service;
        }

        [HttpPost]
        [Authorize(Policy = nameof(PermissionCode.ManageCategories))]
        public async Task<ActionResult<ApiResponse<CategoryPurposeResponseDto>>> Post([FromBody] CreateCategoryPurposeDto dto)
        {
            var result = await _categoryPurposeService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll),
                ApiResponse<CategoryPurposeResponseDto>.Created(result));
        }

        [HttpGet]
        [Authorize(Roles = "Administrator, User")]
        public async Task<ActionResult<ApiResponse<List<CategoryPurposeResponseDto>>>> GetAll([FromQuery] bool? isActive)
        {
            var result = await _categoryPurposeService.GetAllAsync(isActive);
            return Ok(ApiResponse<List<CategoryPurposeResponseDto>>.Ok(result));
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = nameof(PermissionCode.ManageCategories))]
        public async Task<ActionResult<ApiResponse<CategoryPurposeResponseDto>>> Put(int id, [FromBody] UpdateCategoryPurposeDto dto)
        {
            var result = await _categoryPurposeService.UpdateAsync(id, dto);
            return Ok(ApiResponse<CategoryPurposeResponseDto>.Ok(result, "CategoryPurpose atualizado"));
        }
    }
}
