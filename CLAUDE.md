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
```

Swagger UI is available at `http://localhost:5090/swagger` or `https://localhost:7072/swagger` when running.

There is no test project in this solution (`Family-Vault-Api.sln` contains only `FamilyVaultApi.csproj`).

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
- Global exception handling via `Middleware/ExceptionMiddleware.cs` — throw `BadRequestException` from Services/Repositories instead of returning error codes. It explicitly catches `BadRequestException` (400), `UnauthorizedAccessException` (401), `KeyNotFoundException` (404), and `NotFoundException` (`Exceptions/NotFoundException.cs`, 404). Other exception types (e.g. `SecurityTokenException`, `ArgumentException`, `InvalidOperationException`) are not explicitly caught and fall through to the generic 500 handler.
- API versioning is configured via `Asp.Versioning.Mvc`/`Asp.Versioning.Mvc.ApiExplorer` in `Program.cs` (`AddApiVersioning`/`AddApiExplorer`), reading the version from a query string (`api-version`), header (`X-Version`), or media type (`ver`); default version is 1.0 when unspecified.
- Authorization mixes two mechanisms on controller actions: role-based (`[Authorize(Roles = "...")]`) and permission-based (`[Authorize(Policy = nameof(PermissionCode.X))]`, where `PermissionCode` is an enum in `Models/Internal/Enums/PermissionCode.cs` and claim types are in `AppClaimTypes.cs`). Permission claims are attached to roles and seeded via a dedicated migration (`SeedAdministratorPermissionClaims`).
- `AddOData()` is registered globally in `Program.cs` (`.Select().Filter().OrderBy()`) but is currently unused dead configuration — no controller or action uses `[EnableQuery]`/`ODataQueryOptions`, and no `$filter`/`$orderby`/`$select` query syntax is parsed anywhere. Category and Transaction GET endpoints instead use hand-rolled `[FromQuery]` DTOs (`CategoryQueryRequestDto`, `TransactionQueryRequestDto`, extending `PagedFilterDto`) with plain properties (`PageNumber`, `PageSize`, `StartDate`/`EndDate`, `MinAmount`/`MaxAmount`, etc.) bound via normal ASP.NET model binding.

## Domain Model

Financial management app with two user roles:
- **Administrator** — registers via email+password; manages categories and all users
- **User** — registers via Brazilian phone+password; creates and views own transactions

Core entities: `User` (extends `IdentityUser`) → `Transaction` → `Category` → `CategoryPurpose`. Lookup tables `TransactionType` and `CategoryPurpose`, plus initial `Category` rows, are seeded in the initial migration.

EF Core table naming is mixed: Identity/user tables use a `tb_` prefix (`tb_user`, `tb_roles`, `tb_user_roles`, `tb_user_claims`, `tb_user_logins`, `tb_user_tokens`, `tb_role_claims`, configured in `Data/DatabaseContext.cs`), while domain entity tables use plain plural snake_case with **no** prefix (`categories`, `category_purposes`, `transactions`, `transaction_types`, configured via Fluent API in `Data/Configurations/`).

## Authentication

JWT Bearer tokens (10-minute expiry, `JwtSettings:DurationInMinutes`) + refresh tokens backed by ASP.NET Core Identity token provider (database-stored).

- JWT claims: `uid` (user ID), `email` or `phone_number` (whichever the user registered with), `SecurityStamp`, role claims, and permission claims (`AppClaimTypes.Permission`) pulled from the user's role via `RoleManager.GetClaimsAsync`.
- Web clients: refresh token is set as an `HttpOnly` cookie. Mobile clients: refresh token is returned in the response body.
- The web-vs-mobile check is inconsistent between endpoints: `AccountController.Login` decides by whether `loginDto.Email` is set (email login → cookie, refresh token stripped from body), while `AccountController.RefreshToken` decides by checking whether the `User-Agent` header contains `"Mozilla"`. Keep this in mind when touching refresh-token flows.

Token generation logic is in `Repositories/Repository/AccountRepository.cs`. Note: the JWT Bearer `OnChallenge` handler in `Program.cs` overrides the default response to return a `401` with a custom JSON body (`{"message":"Acesso negado."}`) for any missing/invalid token.

## Configuration

All settings live in `appsettings.json` / `appsettings.Development.json`. Key sections:

- `ConnectionStrings:DefaultConnection` — MySQL connection string (database: `FamilyVault`, default port 3306)
- `JwtSettings` — `Issuer`, `Audience`, `DurationInMinutes`, `Key`

CORS is configured (policy `"WebClient"`) to allow `http://localhost:5173` (expected frontend origin).

## Validators

`Common/Validators/` contains regex-based `EmailValidator` and Brazilian phone format `PhoneValidator`. Phone format is enforced via `PhoneValidatorCustomAttribute` (in `Common/Validators/DtoValidators/`) on request DTOs.

## Nullable / Language Settings

Nullable reference types are enabled project-wide. Implicit usings are on — no need to add common `using` statements manually. Code comments and standard messages (`Common/StandardMessages.cs`) are in Portuguese.

## Notes on README.md

The repo has a `README.md` with a full endpoints table (Account, Category, Transaction, User routes with required roles) that's worth checking for route/role reference. Its setup instructions are stale, though: it references a `FamilyVaultDb` database name and port 5001 for Swagger — the actual values are `FamilyVault` and the ports documented above. It also lists Serilog logging, health checks, and rate limiting as features; the related packages are referenced in the `.csproj` but are not actually wired up in `Program.cs` (no `AddHealthChecks()`, no rate-limit middleware, no Serilog sink configuration).
