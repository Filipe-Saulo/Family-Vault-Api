using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
    {
        public void Configure(EntityTypeBuilder<IdentityRole> builder)
        {
            builder.HasData(
                new IdentityRole
                {
                    Id = "5d92e12f-f00c-4899-ac98-89ea76712171",
                    Name = "Administrator",
                    NormalizedName = "ADMINISTRATOR",
                    ConcurrencyStamp = "8f3c1a52-6b7e-4d19-9a40-2c5e7d1b3f68"
                },
                new IdentityRole
                {
                    Id = "bad15740-437c-4721-839c-a9af6fbd3961",
                    Name = "User",
                    NormalizedName = "USER",
                    ConcurrencyStamp = "e2b94d07-1c58-4a63-b7f1-5d8a0c3e9246"
                }
            );
        }
    }
}
