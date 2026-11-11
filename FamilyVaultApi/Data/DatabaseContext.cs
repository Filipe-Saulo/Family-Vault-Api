using FamilyVaultApi.Common;
using FamilyVaultApi.Data.Configurations;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal.Enums;
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
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<VerificationCode> VerificationCodes { get; set; }
        public DbSet<VerificationSendLimit> VerificationSendLimits { get; set; }
        public DbSet<InboundDocument> InboundDocuments { get; set; }
        public DbSet<Charge> Charges { get; set; }
        public DbSet<GmailConnection> GmailConnections { get; set; }
        public DbSet<GmailScan> GmailScans { get; set; }
        public DbSet<GmailIgnoredSender> GmailIgnoredSenders { get; set; }
        public DbSet<GmailOAuthState> GmailOAuthStates { get; set; }
        public DbSet<PersonalAccessToken> PersonalAccessTokens { get; set; }

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
            modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
            modelBuilder.ApplyConfiguration(new VerificationCodeConfiguration());
            modelBuilder.ApplyConfiguration(new VerificationSendLimitConfiguration());
            modelBuilder.ApplyConfiguration(new InboundDocumentConfiguration());
            modelBuilder.ApplyConfiguration(new ChargeConfiguration());
            modelBuilder.ApplyConfiguration(new GmailConnectionConfiguration());
            modelBuilder.ApplyConfiguration(new GmailScanConfiguration());
            modelBuilder.ApplyConfiguration(new GmailIgnoredSenderConfiguration());
            modelBuilder.ApplyConfiguration(new GmailOAuthStateConfiguration());
            modelBuilder.ApplyConfiguration(new PersonalAccessTokenConfiguration());

            SeedData(modelBuilder);           

        }

        // Data fixa dos seeds: com UtcNow cada migration nova geraria UpdateData sem mudança real
        private static readonly DateTime SeedDate = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);

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
                    CreatedAt = SeedDate
                },
                new CategoryPurpose
                {
                    CategoryPurposeId = 2,
                    Code = "income",
                    Name = "Receita",
                    Description = "Apenas para receitas",
                    IsActive = true,
                    CreatedAt = SeedDate
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
                    CreatedAt = SeedDate
                },
                new TransactionType
                {
                    TransactionTypeId = 2,
                    Code = "income",
                    Name = "Receita",
                    Description = "Entrada de recursos",
                    IsActive = true,
                    CreatedAt = SeedDate
                }
            );

            // Seed initial categories
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    CategoryId = 1,
                    Description = "Salário",
                    CategoryPurposeId = 2, // income
                    CreatedAt = SeedDate
                },
                new Category
                {
                    CategoryId = 2,
                    Description = "Alimentação",
                    CategoryPurposeId = 1, // expense
                    CreatedAt = SeedDate
                },
                new Category
                {
                    CategoryId = 3,
                    Description = "Transporte",
                    CategoryPurposeId = 1, // expense
                    CreatedAt = SeedDate
                },
                new Category
                {
                    CategoryId = 4,
                    Description = "Aporte em Investimentos",
                    CategoryPurposeId = 1, // expense
                    CreatedAt = SeedDate
                },
                new Category
                {
                    CategoryId = 5,
                    Description = "Rendimento em Investimentos",
                    CategoryPurposeId = 2, // income
                    CreatedAt = SeedDate
                },
                new Category
                {
                    CategoryId = 6,
                    Description = "Lazer",
                    CategoryPurposeId = 1, // expense
                    CreatedAt = SeedDate
                }
            );

            // Seed Administrator role permission claims
            const string administratorRoleId = "5d92e12f-f00c-4899-ac98-89ea76712171";
            modelBuilder.Entity<IdentityRoleClaim<string>>().HasData(
                new IdentityRoleClaim<string> { Id = 1, RoleId = administratorRoleId, ClaimType = AppClaimTypes.Permission, ClaimValue = nameof(PermissionCode.ManageCategories) },
                new IdentityRoleClaim<string> { Id = 2, RoleId = administratorRoleId, ClaimType = AppClaimTypes.Permission, ClaimValue = nameof(PermissionCode.ManageTransactionTypes) },
                new IdentityRoleClaim<string> { Id = 3, RoleId = administratorRoleId, ClaimType = AppClaimTypes.Permission, ClaimValue = nameof(PermissionCode.ManageTransactions) },
                new IdentityRoleClaim<string> { Id = 4, RoleId = administratorRoleId, ClaimType = AppClaimTypes.Permission, ClaimValue = nameof(PermissionCode.ManageUsers) }
            );
        }
    }
}
