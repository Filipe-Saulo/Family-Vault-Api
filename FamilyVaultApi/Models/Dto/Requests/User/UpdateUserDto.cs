using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.User
{
    public class UpdateUserDto
    {
        [Required(ErrorMessage = "O primeiro nome é obrigatório.")]
        [StringLength(100, ErrorMessage = "O Primeiro nome deve ter no máximo 100 caracteres.")]
        public string FirstName { get; set; }
        [Required(ErrorMessage = "O Sobrenome é obrigatório.")]
        [StringLength(100, ErrorMessage = "O Sobrenome deve ter no máximo 100 caracteres.")]
        public string LastName { get; set; }
        [Range(0, 150, ErrorMessage = "A Idade deve estar entre 0 e 150.")]
        public int Age { get; set; }
    }
}
