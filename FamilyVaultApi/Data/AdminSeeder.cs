using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace FamilyVaultApi.Data
{
    public static class AdminSeeder
    {
        private const string AdministratorRole = "Administrator";

        // Cria o primeiro Administrator a partir de AdminSeed:Email / AdminSeed:Password, só se ainda não houver nenhum.
        public static async Task SeedAdministratorAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");

            try
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                // Os papéis são semeados pela migration (RoleConfiguration)
                if (!await roleManager.RoleExistsAsync(AdministratorRole))
                {
                    logger.LogWarning("Papel {Role} não encontrado; seed do administrador ignorado.", AdministratorRole);
                    return;
                }

                if ((await userManager.GetUsersInRoleAsync(AdministratorRole)).Any())
                    return;

                var email = configuration["AdminSeed:Email"];
                var password = configuration["AdminSeed:Password"];

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    logger.LogWarning("Nenhum Administrator cadastrado e AdminSeed:Email/AdminSeed:Password não configurados.");
                    return;
                }

                var user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "Family Vault",
                    FullName = "Admin Family Vault",
                    RegisteredAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow,
                    Age = 18
                };

                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    logger.LogError("Não foi possível criar o administrador inicial: {Errors}",
                        string.Join(" | ", created.Errors.Select(e => e.Code)));
                    return;
                }

                var inRole = await userManager.AddToRoleAsync(user, AdministratorRole);
                if (!inRole.Succeeded)
                {
                    await userManager.DeleteAsync(user);
                    logger.LogError("Não foi possível atribuir o papel {Role} ao administrador inicial.", AdministratorRole);
                    return;
                }

                logger.LogInformation("Administrator inicial criado.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao executar o seed do administrador.");
            }
        }
    }
}
