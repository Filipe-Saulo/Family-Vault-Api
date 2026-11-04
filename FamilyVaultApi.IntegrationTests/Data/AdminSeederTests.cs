using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;

namespace FamilyVaultApi.IntegrationTests.Data
{
    public class AdminSeederTests
    {
        private const string Email = "admin@integration.test";
        private const string Password = "Senha@Forte123";

        [Fact]
        public async Task SeedAdministratorAsync_WhenNoAdministratorAndConfigured_ShouldCreateAdministrator()
        {
            using var factory = new FamilyVaultApiFactory(Email, Password);

            var admins = await factory.GetAdministratorsAsync();

            admins.Should().ContainSingle();
            admins[0].Email.Should().Be(Email);
            admins[0].EmailConfirmed.Should().BeTrue();
            (await factory.CheckPasswordAsync(admins[0], Password)).Should().BeTrue();
        }

        [Fact]
        public async Task SeedAdministratorAsync_WhenAdministratorAlreadyExists_ShouldNotCreateAnother()
        {
            using var factory = new FamilyVaultApiFactory(Email, Password, SeedExistingAdministrator);

            var admins = await factory.GetAdministratorsAsync();

            admins.Should().ContainSingle();
            admins[0].Email.Should().Be("existente@integration.test");
        }

        [Theory]
        [InlineData(null, Password)]
        [InlineData(Email, null)]
        public async Task SeedAdministratorAsync_WhenSeedNotConfigured_ShouldNotCreateAdministrator(string? email, string? password)
        {
            using var factory = new FamilyVaultApiFactory(email, password);

            var admins = await factory.GetAdministratorsAsync();

            admins.Should().BeEmpty();
        }

        [Fact]
        public async Task SeedAdministratorAsync_WhenPasswordIsRejectedByIdentity_ShouldNotCreateAdministrator()
        {
            using var factory = new FamilyVaultApiFactory(Email, "fraca");

            var admins = await factory.GetAdministratorsAsync();

            admins.Should().BeEmpty();
        }

        private static void SeedExistingAdministrator(DatabaseContext context)
        {
            var role = context.Roles.Single(r => r.Name == "Administrator");
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "existente@integration.test",
                NormalizedUserName = "EXISTENTE@INTEGRATION.TEST",
                Email = "existente@integration.test",
                NormalizedEmail = "EXISTENTE@INTEGRATION.TEST",
                SecurityStamp = Guid.NewGuid().ToString(),
                FirstName = "Admin",
                LastName = "Existente",
                FullName = "Admin Existente",
                Age = 30
            };

            context.Users.Add(user);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
            context.SaveChanges();
        }
    }
}
