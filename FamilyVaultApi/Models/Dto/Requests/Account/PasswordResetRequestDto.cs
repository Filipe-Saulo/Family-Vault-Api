using FamilyVaultApi.Common.Validators.DtoValidators;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Requests.Account
{
    public class PasswordResetRequestDto
    {
        [PhoneValidatorCustomRequired]
        public string Phone { get; set; }
        [Required(ErrorMessage = "Senha é obrigatório")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Confirmação de senha é obrigatório")]
        public string PasswordConfirm { get; set; }                        
    }
}
