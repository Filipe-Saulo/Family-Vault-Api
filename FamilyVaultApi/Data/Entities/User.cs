using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace FamilyVaultApi.Data.Entities
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }        
        public string FullName { get; set; }
        public int Age { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime LastLogin { get; set; }

        public virtual ICollection<Transaction> Transactions { get; set; }

    }
}
