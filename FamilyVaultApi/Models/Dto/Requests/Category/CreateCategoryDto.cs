using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Category
{
    public class CreateCategoryDto
    {
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(255, ErrorMessage = "A descrição deve ter no máximo 255 caracteres.")]
        public string Description { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "A finalidade da categoria é obrigatória.")]
        public int CategoryPurposeId { get; set; }
    }
}
