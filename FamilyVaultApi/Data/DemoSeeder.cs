using FamilyVaultApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FamilyVaultApi.Data
{
    // Conta da demonstração pública: um Administrator só leitura (DemoReadOnlyMiddleware) e uma família fictícia com
    // seis meses de transações. Liga com DemoSeed:Enabled; e-mail e senha da conta demo vêm de DemoSeed:Email e
    // DemoSeed:Password. Roda na subida, depois do AdminSeeder, e é idempotente: cria o que falta e, uma vez por dia,
    // refaz as transações com datas relativas a hoje (o dashboard abre no mês corrente). Como o gerador usa uma
    // semente por mês, os dias que já passaram saem iguais a cada refação.
    public static class DemoSeeder
    {
        private const string AdministratorRole = "Administrator";
        private const string UserRole = "User";
        private const int ExpensePurposeId = 1;
        private const int IncomePurposeId = 2;
        private const int ExpenseTypeId = 1;
        private const int IncomeTypeId = 2;
        public const int MonthsOfHistory = 6;

        public static readonly IReadOnlyList<DemoMember> Members = new[]
        {
            new DemoMember("+5541900000001", "Ana", "Souza", 38),
            new DemoMember("+5541900000002", "Bruno", "Souza", 40),
            new DemoMember("+5541900000003", "Clara", "Souza", 16)
        };

        public static async Task SeedDemoAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoSeeder");
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            if (!configuration.GetValue<bool>("DemoSeed:Enabled"))
                return;

            try
            {
                await SeedAsync(scope.ServiceProvider, configuration, DateTime.UtcNow, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao executar o seed da conta demo.");
            }
        }

        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, DateTime utcNow, ILogger logger)
        {
            var email = configuration["DemoSeed:Email"]?.Trim().ToLowerInvariant();
            var password = configuration["DemoSeed:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("DemoSeed:Enabled sem DemoSeed:Email/DemoSeed:Password; seed da demo ignorado.");
                return;
            }

            var userManager = services.GetRequiredService<UserManager<User>>();
            var context = services.GetRequiredService<DatabaseContext>();

            if (!await EnsureDemoAdministratorAsync(userManager, email, password, utcNow, logger))
                return;

            var members = new List<User>();
            foreach (var member in Members)
            {
                var user = await EnsureMemberAsync(userManager, member, utcNow, logger);
                if (user == null)
                    return;
                members.Add(user);
            }

            var today = DateOnly.FromDateTime(utcNow);
            var memberIds = members.Select(m => m.Id).ToList();

            // Refaz no máximo uma vez por dia
            var lastCreated = await context.Transactions
                .Where(t => memberIds.Contains(t.UserId))
                .MaxAsync(t => (DateTime?)t.CreatedAt);
            if (lastCreated.HasValue && DateOnly.FromDateTime(lastCreated.Value) >= today)
                return;

            var categories = await EnsureCategoriesAsync(context, members, utcNow);
            var transactions = BuildTransactions(members, categories, today, utcNow);

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await context.Database.BeginTransactionAsync();
                await context.Transactions.Where(t => memberIds.Contains(t.UserId)).ExecuteDeleteAsync();
                context.Transactions.AddRange(transactions);
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            });
            context.ChangeTracker.Clear();

            logger.LogInformation("Dados da demo refeitos: {Count} transações.", transactions.Count);
        }

        private static async Task<bool> EnsureDemoAdministratorAsync(UserManager<User> userManager, string email, string password, DateTime utcNow, ILogger logger)
        {
            var user = await userManager.FindByNameAsync(email);
            if (user != null && !user.IsDemo)
            {
                // Nunca transforma uma conta real em demo
                logger.LogError("DemoSeed:Email já pertence a uma conta que não é da demo; seed da demo ignorado.");
                return false;
            }

            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "Demo",
                    LastName = "Family Vault",
                    FullName = "Demo Family Vault",
                    Age = 30,
                    RegisteredAt = utcNow,
                    LastLogin = utcNow,
                    IsDemo = true
                };

                if (!await CreateInRoleAsync(userManager, user, password, AdministratorRole, logger))
                    return false;

                logger.LogInformation("Conta demo criada.");
                return true;
            }

            // A senha é pública: com lockout, qualquer um bloquearia a demo errando a senha
            if (user.LockoutEnabled)
                await userManager.SetLockoutEnabledAsync(user, false);

            // A senha publicada é a da configuração
            if (!await userManager.CheckPasswordAsync(user, password))
            {
                await userManager.RemovePasswordAsync(user);
                var reset = await userManager.AddPasswordAsync(user, password);
                if (!reset.Succeeded)
                {
                    logger.LogError("Não foi possível atualizar a senha da conta demo: {Errors}",
                        string.Join(" | ", reset.Errors.Select(e => e.Code)));
                    return false;
                }
            }

            return true;
        }

        private static async Task<User?> EnsureMemberAsync(UserManager<User> userManager, DemoMember member, DateTime utcNow, ILogger logger)
        {
            var user = await userManager.FindByNameAsync(member.Phone);
            if (user != null)
            {
                if (user.IsDemo)
                {
                    if (user.LockoutEnabled)
                        await userManager.SetLockoutEnabledAsync(user, false);
                    return user;
                }

                logger.LogError("O telefone de um membro da demo já pertence a uma conta real; seed da demo ignorado.");
                return null;
            }

            user = new User
            {
                UserName = member.Phone,
                PhoneNumber = member.Phone,
                PhoneNumberConfirmed = true,
                FirstName = member.FirstName,
                LastName = member.LastName,
                FullName = $"{member.FirstName} {member.LastName}",
                Age = member.Age,
                RegisteredAt = utcNow.AddMonths(-MonthsOfHistory),
                LastLogin = utcNow,
                IsDemo = true
            };

            // Ninguém entra como membro: a senha é aleatória e não fica guardada
            return await CreateInRoleAsync(userManager, user, RandomPassword(), UserRole, logger) ? user : null;
        }

        private static async Task<bool> CreateInRoleAsync(UserManager<User> userManager, User user, string password, string role, ILogger logger)
        {
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Não foi possível criar um usuário da demo: {Errors}",
                    string.Join(" | ", created.Errors.Select(e => e.Code)));
                return false;
            }

            // O CreateAsync liga o lockout de toda conta nova (Lockout.AllowedForNewUsers)
            await userManager.SetLockoutEnabledAsync(user, false);

            var inRole = await userManager.AddToRoleAsync(user, role);
            if (!inRole.Succeeded)
            {
                await userManager.DeleteAsync(user);
                logger.LogError("Não foi possível atribuir o papel {Role} a um usuário da demo.", role);
                return false;
            }

            return true;
        }

        private static string RandomPassword() =>
            $"Dm{Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))}9!a";

        // Categorias pessoais de cada membro (as globais vêm da migration e são só lidas)
        private static readonly (string Description, int PurposeId)[] PersonalCategories =
        {
            ("Moradia", ExpensePurposeId),
            ("Saúde", ExpensePurposeId),
            ("Contas da casa", ExpensePurposeId),
            ("Freelance", IncomePurposeId),
            ("Mesada", IncomePurposeId)
        };

        private static async Task<Dictionary<(string UserId, string Description), int>> EnsureCategoriesAsync(DatabaseContext context, List<User> members, DateTime utcNow)
        {
            var memberIds = members.Select(m => m.Id).ToList();
            var existing = await context.Categories
                .Where(c => c.UserId == null || memberIds.Contains(c.UserId))
                .Select(c => new { c.CategoryId, c.Description, c.UserId })
                .ToListAsync();

            var result = new Dictionary<(string, string), int>();
            foreach (var member in members)
            {
                foreach (var global in existing.Where(c => c.UserId == null))
                    result[(member.Id, global.Description)] = global.CategoryId;

                foreach (var (description, purposeId) in PersonalCategories)
                {
                    var own = existing.FirstOrDefault(c => c.UserId == member.Id && c.Description == description);
                    if (own != null)
                    {
                        result[(member.Id, description)] = own.CategoryId;
                        continue;
                    }

                    var category = new Category { Description = description, CategoryPurposeId = purposeId, UserId = member.Id, CreatedAt = utcNow };
                    context.Categories.Add(category);
                    await context.SaveChangesAsync();
                    result[(member.Id, description)] = category.CategoryId;
                }
            }

            return result;
        }

        // Lançamento do mês: dia, categoria, descrição, valor mínimo e máximo, e chance de aparecer no mês
        private sealed record Entry(int Day, string Category, string Description, decimal Min, decimal Max, bool Income = false, double Chance = 1);

        private static readonly Entry[][] MonthlyEntries =
        {
            // Ana
            new[]
            {
                new Entry(5, "Salário", "Salário", 7800, 7800, Income: true),
                new Entry(10, "Moradia", "Aluguel", 2300, 2300),
                new Entry(10, "Aporte em Investimentos", "Aporte no Tesouro Selic", 800, 800),
                new Entry(15, "Saúde", "Plano de saúde", 689.90m, 689.90m),
                new Entry(6, "Alimentação", "Supermercado", 380, 520),
                new Entry(13, "Alimentação", "Supermercado", 250, 410),
                new Entry(20, "Alimentação", "Feira", 90, 160),
                new Entry(27, "Alimentação", "Supermercado", 280, 460),
                new Entry(8, "Transporte", "Combustível", 180, 260),
                new Entry(22, "Transporte", "Combustível", 170, 240),
                new Entry(18, "Lazer", "Jantar fora", 140, 260, Chance: 0.7),
                new Entry(25, "Saúde", "Farmácia", 45, 130, Chance: 0.6),
                new Entry(28, "Rendimento em Investimentos", "Rendimento do Tesouro Selic", 38, 72, Income: true)
            },
            // Bruno
            new[]
            {
                new Entry(5, "Salário", "Salário", 5400, 5400, Income: true),
                new Entry(12, "Freelance", "Projeto freelance", 600, 1500, Income: true, Chance: 0.6),
                new Entry(9, "Contas da casa", "Conta de luz", 170, 265),
                new Entry(11, "Contas da casa", "Internet", 119.90m, 119.90m),
                new Entry(16, "Contas da casa", "Conta de água", 85, 140),
                new Entry(3, "Lazer", "Streaming", 55.90m, 55.90m),
                new Entry(21, "Lazer", "Cinema", 60, 110, Chance: 0.5),
                new Entry(7, "Alimentação", "Restaurante", 45, 120),
                new Entry(14, "Alimentação", "Restaurante", 40, 95),
                new Entry(24, "Alimentação", "Delivery", 55, 130),
                new Entry(4, "Transporte", "Aplicativo de transporte", 18, 45),
                new Entry(17, "Transporte", "Aplicativo de transporte", 18, 45),
                new Entry(26, "Transporte", "Estacionamento", 25, 60, Chance: 0.6)
            },
            // Clara
            new[]
            {
                new Entry(1, "Mesada", "Mesada", 400, 400, Income: true),
                new Entry(2, "Transporte", "Recarga do cartão de ônibus", 120, 120),
                new Entry(9, "Alimentação", "Lanche", 20, 45),
                new Entry(19, "Alimentação", "Lanche", 18, 40),
                new Entry(12, "Lazer", "Cinema com amigas", 35, 70, Chance: 0.6),
                new Entry(23, "Lazer", "Livro", 40, 90, Chance: 0.5)
            }
        };

        private static List<Transaction> BuildTransactions(List<User> members, Dictionary<(string UserId, string Description), int> categories, DateOnly today, DateTime utcNow)
        {
            var result = new List<Transaction>();
            var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(MonthsOfHistory - 1));

            for (var m = 0; m < MonthsOfHistory; m++)
            {
                var month = firstMonth.AddMonths(m);
                for (var i = 0; i < members.Count; i++)
                {
                    // Semente fixa por mês e membro: refazer no mesmo mês gera os mesmos valores
                    var random = new Random(month.Year * 1000 + month.Month * 10 + i);
                    foreach (var entry in MonthlyEntries[i])
                    {
                        var appears = random.NextDouble() < entry.Chance;
                        var amount = entry.Min + (decimal)random.NextDouble() * (entry.Max - entry.Min);

                        var day = Math.Min(entry.Day, DateTime.DaysInMonth(month.Year, month.Month));
                        var date = new DateOnly(month.Year, month.Month, day);
                        if (!appears || date > today)
                            continue;
                        if (!categories.TryGetValue((members[i].Id, entry.Category), out var categoryId))
                            continue;

                        result.Add(new Transaction
                        {
                            UserId = members[i].Id,
                            CategoryId = categoryId,
                            TransactionTypeId = entry.Income ? IncomeTypeId : ExpenseTypeId,
                            Description = entry.Description,
                            Amount = Math.Round(amount, 2),
                            TransactionDate = date.ToDateTime(TimeOnly.MinValue),
                            CreatedAt = utcNow
                        });
                    }
                }
            }

            return result;
        }

        public sealed record DemoMember(string Phone, string FirstName, string LastName, int Age);
    }
}
