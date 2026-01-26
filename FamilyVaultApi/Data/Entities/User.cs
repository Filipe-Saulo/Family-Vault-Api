using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace FamilyVaultApi.Data.Entities
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }
        
        public string FullName { get; set; }

        public DateTime RegisteredAt { get; set; }

        public DateTime LastLogin { get; set; }
        
        public string RefreshToken { get; set; }

        public string DocumentType { get; set; }
        
        public string DocumentNumber { get; set; }        

    }
}
