using AspNetCoreRateLimit;
using FamilyVaultApi.Common;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Mapping;
using FamilyVaultApi.Middleware;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Repositories.Repository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Fail fast, before anything touches the database.
var jwtKey = builder.Configuration["JwtSettings:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        "JwtSettings:Key ausente ou com menos de 32 bytes. Em desenvolvimento use 'dotnet user-secrets set \"JwtSettings:Key\" \"<32+ caracteres>\"'; nos demais ambientes, a variável JwtSettings__Key.");


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// No ambiente "Testing" o teste de integração registra o próprio banco (SQLite em memória);
// o AutoDetect abriria conexão com o MySQL aqui.
if (!builder.Environment.IsEnvironment("Testing"))
{
    try
    {
        ServerVersion serverVersion = ServerVersion.AutoDetect(connectionString);  // Automatically detect MySQL version
        Console.WriteLine("MySQL server version detected successfully.");

        builder.Services.AddDbContext<DatabaseContext>(options =>
          options.UseMySql(connectionString, serverVersion, mysqlOptions =>
          {
              mysqlOptions.EnableRetryOnFailure(
                  maxRetryCount: 5, // Retry up to 5 times
                  maxRetryDelay: TimeSpan.FromSeconds(10), // Max delay between retries
                  errorNumbersToAdd: null // Specify error codes to add to the retry list
              );
          })
      );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while detecting the MySQL version: {ex.Message}");
        throw;
    }
}

builder.Services.AddHttpContextAccessor();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Family Vault", Version = "v1" });
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. 
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme,
                                Id = JwtBearerDefaults.AuthenticationScheme
                            },
                Scheme = "0auth2",
                Name = JwtBearerDefaults.AuthenticationScheme,
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy
        .WithOrigins(
        "http://localhost:5173" 
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Host.UseSerilog((ctx, lc) => lc.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddErrorDescriber<PortugueseIdentityErrorDescriber>()
.AddEntityFrameworkStores<DatabaseContext>()
.AddDefaultTokenProviders();

builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
//account
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<IPhoneNumberService, PhoneNumberService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IVerificationCodeSender, LoggingVerificationCodeSender>();
//user
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
//category
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
//category purpose
builder.Services.AddScoped<ICategoryPurposeService, CategoryPurposeService>();
builder.Services.AddScoped<ICategoryPurposeRepository, CategoryPurposeRepository>();
//transaction
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
//transaction type
builder.Services.AddScoped<ITransactionTypeService, TransactionTypeService>();
builder.Services.AddScoped<ITransactionTypeRepository, TransactionTypeRepository>();
//dashboard
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();

builder.Services.AddAuthentication(options =>
{    
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),
            RoleClaimType = ClaimTypes.Role,
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ApiResponseWriter.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Acesso negado.");
            },
            OnForbidden = context =>
                ApiResponseWriter.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Você não tem permissão para acessar este recurso.")
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(nameof(PermissionCode.ManageCategories), p => p.RequireClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageCategories)));
    options.AddPolicy(nameof(PermissionCode.ManageTransactionTypes), p => p.RequireClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageTransactionTypes)));
    options.AddPolicy(nameof(PermissionCode.ManageTransactions), p => p.RequireClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageTransactions)));
    options.AddPolicy(nameof(PermissionCode.ManageUsers), p => p.RequireClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageUsers)));
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var invalid = context.ModelState.Where(kvp => kvp.Value?.Errors.Count > 0).ToList();

            // Erro de conversão do JSON (chave "$" ou "$.campo"): a mensagem do System.Text.Json é em inglês e
            // vem acompanhada de um "The x field is required." do parâmetro; responde só a mensagem traduzida.
            var jsonErrors = invalid
                .Where(kvp => kvp.Key.StartsWith('$'))
                .Select(kvp => kvp.Key.TrimStart('$', '.'))
                .Select(field => field.Length == 0
                    ? "Corpo da requisição inválido."
                    : $"Valor inválido para o campo '{field}'.")
                .ToList();

            var errors = jsonErrors.Count > 0
                ? jsonErrors
                : invalid.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage));

            var response = new ApiResponse<object>(string.Join(" | ", errors))
            {
                TraceId = context.HttpContext.TraceIdentifier
            };

            return new BadRequestObjectResult(response);
        };
    });

builder.Services.AddHealthChecks()
    .AddMySql(connectionString, name: "mysql");

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

await app.SeedAdministratorAsync();

app.UseMiddleware<ApiIpRateLimitMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseCors("WebClient");

app.Use(async (context, next) =>
{
    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "no-store";

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Expõe o Program para o WebApplicationFactory dos testes de integração.
public partial class Program { }
