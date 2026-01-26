using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.Models.Dto.Responses.User
{
    public class UserResponseDto
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }

        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }
    }
}
