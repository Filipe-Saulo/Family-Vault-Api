# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build
dotnet build --configuration Release

# Run (HTTP: localhost:5090, HTTPS: localhost:7072)
dotnet run --project FamilyVaultApi

# Database migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update
dotnet ef database drop

# Unit tests
dotnet test FamilyVaultApi.UnitTests
```

Swagger UI is available at `http://localhost:5090/swagger` or `https://localhost:7072/swagger` when running.

`Family-Vault-Api.sln` has two projects: `FamilyVaultApi` (the API) and `FamilyVaultApi.UnitTests` (xUnit + Moq + FluentAssertions + Bogus, `net8.0`, `ProjectReference` to the main project). Tests live under `Services/` (one file per `Services/Service/*.cs` class), with mocks built per-test-class in the constructor (no shared fixtures) and fluent request builders in `Builders/<Entity>/` (`XxxDtoBuilder.New().With...().Build()`, defaults generated via `Bogus.Faker` rather than fixed constants). Naming convention: `MethodName_Scenario_ExpectedResult`. Only the Service layer has coverage — Controllers and Repositories do not.

## Architecture

ASP.NET Core 8 Web API using a strict 4-layer architecture:

```
Controller → Service → Repository → EF Core (MySQL)
```

Each layer has a matching interface under `IService/` and `IRepository/`. Register new services/repos in `Program.cs`.

**Key patterns:**
- All controllers return `ApiResponse<T>` (defined in `Common/ApiResponse.cs`)
- Business logic lives in Services; data access in Repositories
- AutoMapper handles all Entity ↔ DTO conversions (profile in `Mapping/AutoMapperProfile.cs`)
- Global exception handling via `Middleware/ExceptionMiddleware.cs` — throw `BadRequestException` from Services/Repositories instead of returning error codes. It explicitly catches `BadRequestException` (400), `UnauthorizedAccessException` (401), `KeyNotFoundException` (404), `NotFoundException` (`Exceptions/NotFoundException.cs`, 404), and `System.Security.SecurityException` (403). Other exception types (e.g. `SecurityTokenException`, `ArgumentException`, `InvalidOperationException`) are not explicitly caught and fall through to the generic 500 handler. Every response (including errors) uses the `ApiResponse<T>` envelope with a `TraceId` set from `HttpContext.TraceIdentifier`, and errors are logged with that same trace ID for correlation.
- API versioning is configured via `Asp.Versioning.Mvc`/`Asp.Versioning.Mvc.ApiExplorer` in `Program.cs` (`AddApiVersioning`/`AddApiExplorer`), reading the version from a query string (`api-version`), header (`X-Version`), or media type (`ver`); default version is 1.0 when unspecified.
- Authorization mixes three mechanisms: role-based (`[Authorize(Roles = "...")]`), permission-based (`[Authorize(Policy = nameof(PermissionCode.X))]`, where `PermissionCode` is an enum in `Models/Internal/Enums/PermissionCode.cs` and claim types are in `AppClaimTypes.cs`), and resource-ownership checks done inside the Service layer rather than via `[Authorize]`. For actions like updating/deleting a User or Transaction, the controller only requires the `Administrator`/`User` role and passes the `ClaimsPrincipal` into the service; the service allows Administrators and permission-policy holders to act on any record, and otherwise throws `NotFoundException` (record doesn't exist) or `System.Security.SecurityException` (403, caller isn't the owner) — see `TransactionService.EnsureCanModifyTransactionAsync` and the equivalent ownership check in `UserService`. Permission claims are attached to roles and seeded via a dedicated migration (`SeedAdministratorPermissionClaims`).
- `AddOData()` is registered globally in `Program.cs` (`.Select().Filter().OrderBy()`) but is currently unused dead configuration — no controller or action uses `[EnableQuery]`/`ODataQueryOptions`, and no `$filter`/`$orderby`/`$select` query syntax is parsed anywhere. Category and Transaction GET endpoints instead use hand-rolled `[FromQuery]` DTOs (`CategoryQueryRequestDto`, `TransactionQueryRequestDto`, extending `PagedFilterDto`) with plain properties (`PageNumber`, `PageSize`, `StartDate`/`EndDate`, `MinAmount`/`MaxAmount`, etc.) bound via normal ASP.NET model binding.

## Domain Model

Financial management app with two user roles:
- **Administrator** — registers via email+password; manages categories and all users
- **User** — registers via Brazilian phone+password; creates and views own transactions

Core entities: `User` (extends `IdentityUser`) → `Transaction` → `Category` → `CategoryPurpose`. Lookup tables `TransactionType` and `CategoryPurpose`, plus initial `Category` rows, are seeded in the initial migration.

`DashboardController` (`GET /api/dashboard/summary`) aggregates transaction totals per `Category`/`TransactionType` for a date range; results are scoped to the caller's own data when the caller is a `User`, and unscoped for `Administrator`.

EF Core table naming is mixed: Identity/user tables use a `tb_` prefix (`tb_user`, `tb_roles`, `tb_user_roles`, `tb_user_claims`, `tb_user_logins`, `tb_user_tokens`, `tb_role_claims`, configured in `Data/DatabaseContext.cs`), while domain entity tables use plain plural snake_case with **no** prefix (`categories`, `category_purposes`, `transactions`, `transaction_types`, configured via Fluent API in `Data/Configurations/`).

## Authentication

JWT Bearer tokens (10-minute expiry, `JwtSettings:DurationInMinutes`) + refresh tokens backed by ASP.NET Core Identity token provider (database-stored).

- JWT claims: `uid` (user ID), `email` or `phone_number` (whichever the user registered with), `SecurityStamp`, role claims, and permission claims (`AppClaimTypes.Permission`) pulled from the user's role via `RoleManager.GetClaimsAsync`.
- Web vs. mobile is selected by route, not by inspecting the request: `AccountController` exposes separate `web/login` + `web/refreshtoken` (refresh token set as an `HttpOnly` cookie, stripped from the response body) and `app/login` + `app/refreshtoken` (refresh token returned in the response body) endpoints. `LoginAsync` separately infers Administrator-vs-User login by which of `loginDto.Email`/`loginDto.Phone` is populated (exactly one must be set) — that check is about credential type, not client platform.

Token generation logic is in `Repositories/Repository/AccountRepository.cs`. Note: the JWT Bearer `OnChallenge` handler in `Program.cs` overrides the default response to return a `401` with a custom JSON body (`{"message":"Acesso negado."}`) for any missing/invalid token.

## Configuration

All settings live in `appsettings.json` / `appsettings.Development.json`. Key sections:

- `ConnectionStrings:DefaultConnection` — MySQL connection string (database: `FamilyVault`, default port 3306)
- `JwtSettings` — `Issuer`, `Audience`, `DurationInMinutes`, `Key`

CORS is configured (policy `"WebClient"`) to allow `http://localhost:5173` (expected frontend origin).

## Validators

`Common/Validators/` contains a regex-based `EmailValidator`. Phone numbers use a two-layer pattern: `PhoneValidatorCustomAttribute`/`PhoneValidatorCustomRequiredAttribute` (`Common/Validators/DtoValidators/`) do a cheap structural check on the DTO (digits/`+`/spaces/parentheses/hyphen, plausible length) with no DI, applied to `CreateAccountRequestDto.PhoneNumber`, `LoginRequestDto.Phone`, and `PasswordResetRequestDto.Phone`; real per-country validation and E.164 normalization happen in `Services/Service/PhoneNumberService.cs` (`IPhoneNumberService`, wraps `libphonenumber-csharp`, registered as a DI singleton in `Program.cs`), called from `AccountService.ValidatePhoneAsync` during registration. Canonical stored phone format is E.164 with a leading `+` (e.g. `+5511987654312`) — `UserName`/`NormalizedUserName`/`PhoneNumber` on `User` must stay in sync with this format since login/refresh do exact-string lookups with no re-normalization.

## Nullable / Language Settings

Nullable reference types are enabled project-wide. Implicit usings are on — no need to add common `using` statements manually. Code comments and standard messages (`Common/StandardMessages.cs`) are in Portuguese.

## Notes on README.md

The repo has a `README.md` with a full endpoints table (Account, Category, Transaction, User, Dashboard routes with required roles) that's worth checking for route/role reference and matches the current controllers. Its setup instructions are stale, though: it references a `FamilyVaultDb` database name and port 5001 for Swagger — the actual values are `FamilyVault` and the ports documented above.

Serilog, health checks, and rate limiting (all listed as README features) **are** wired up in `Program.cs`: `UseSerilog`/`UseSerilogRequestLogging` write to console + a rolling file (`Serilog` section in `appsettings.json`), `AddHealthChecks().AddMySql(...)` backs `GET /health`, and `AddInMemoryRateLimiting`/`UseIpRateLimiting` (AspNetCoreRateLimit, `IpRateLimiting` section) throttles auth endpoints (5-10 req/min on login/register/reset/refresh routes, 429 on breach). Response caching is also configured (`AddResponseCaching`/`UseResponseCaching`, 10s `Cache-Control: public` on all responses). `AddOData()` remains the one genuinely dead registration — see above. The README's Transaction endpoints table still lists PUT/DELETE `/api/transaction/{id}` as requiring the `ManageTransactions` permission — that's now stale: both routes only require the `Administrator`/`User` role, with ownership enforced in `TransactionService` (see Authorization above).

A GitHub Actions workflow (`.github/workflows/ci.yml`) runs `dotnet build` + `dotnet test FamilyVaultApi.UnitTests` on every push/PR to `main`; there is no deploy step (portfolio project, not deployed anywhere).
