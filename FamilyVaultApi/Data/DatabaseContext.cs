using FamilyVaultApi.Data.Configurations;
using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Data
{
    public class DatabaseContext : IdentityDbContext<User, IdentityRole, string>
    {

        public DbSet<CategoryPurpose> CategoryPurposes { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<TransactionType> TransactionTypes { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

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

            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new CategoryPurposeConfiguration());
            modelBuilder.ApplyConfiguration(new CategoryConfiguration());
            modelBuilder.ApplyConfiguration(new TransactionTypeConfiguration());
            modelBuilder.ApplyConfiguration(new TransactionConfiguration());
            modelBuilder.ApplyConfiguration(new RoleConfiguration());

            SeedData(modelBuilder);           

        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed category purposes
            modelBuilder.Entity<CategoryPurpose>().HasData(
                new CategoryPurpose
                {
                    CategoryPurposeId = 1,
                    Code = "expense",
                    Name = "Despesa",
                    Description = "Apenas para despesas",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new CategoryPurpose
                {
                    CategoryPurposeId = 2,
                    Code = "income",
                    Name = "Receita",
                    Description = "Apenas para receitas",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new CategoryPurpose
                {
                    CategoryPurposeId = 3,
                    Code = "both",
                    Name = "Ambas",
                    Description = "Para despesas e receitas",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );

            // Seed transaction types
            modelBuilder.Entity<TransactionType>().HasData(
                new TransactionType
                {
                    TransactionTypeId = 1,
                    Code = "expense",
                    Name = "Despesa",
                    Description = "Saída de recursos",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new TransactionType
                {
                    TransactionTypeId = 2,
                    Code = "income",
                    Name = "Receita",
                    Description = "Entrada de recursos",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );

            // Seed initial categories
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    CategoryId = 1,
                    Description = "Salário",
                    CategoryPurposeId = 2, // income
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    CategoryId = 2,
                    Description = "Alimentação",
                    CategoryPurposeId = 1, // expense
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    CategoryId = 3,
                    Description = "Transporte",
                    CategoryPurposeId = 1, // expense
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    CategoryId = 4,
                    Description = "Investimentos",
                    CategoryPurposeId = 3, // both
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    CategoryId = 5,
                    Description = "Lazer",
                    CategoryPurposeId = 3, // both
                    CreatedAt = DateTime.UtcNow
                }
            );
        }
    }
}
