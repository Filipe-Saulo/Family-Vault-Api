using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Data.Configurations
{
    public class DatabaseContext : IdentityDbContext<User, IdentityRole, string>
    {
        public DatabaseContext(DbContextOptions options) : base(options)
        {

        }  

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);    

            modelBuilder.Entity<User>().ToTable("tb_user");

            modelBuilder.Entity<User>().Property(u => u.Id).HasColumnName("user_id");

            modelBuilder.Entity<IdentityRole>().ToTable("tb_roles");
            modelBuilder.Entity<IdentityUserRole<string>>().ToTable("tb_user_roles");
            modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("tb_user_claims");
            modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("tb_user_logins");
            modelBuilder.Entity<IdentityUserToken<string>>().ToTable("tb_user_tokens");
            modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("tb_role_claims");

            modelBuilder.ApplyConfiguration(new RoleConfiguration());

        }
    }
}
