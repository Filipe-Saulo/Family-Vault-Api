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
- Global exception handling via `Middleware/ExceptionMiddleware.cs` — throw `BadRequestException` from Services/Repositories instead of returning error codes. Note: `NotFoundException` (`Exceptions/NotFoundException.cs`) is thrown by repositories but the middleware only explicitly catches `KeyNotFoundException`, `BadRequestException`, and `UnauthorizedAccessException` — an uncaught `NotFoundException` currently falls through to the generic handler and returns 500 instead of 404. Keep this in mind when touching that code path.
- OData (`$filter`, `$orderby`, `$select`) is wired globally via `AddOData()` in `Program.cs`, not per-controller `[EnableQuery]` attributes — Category and Transaction GET endpoints accept OData query params through their `[FromQuery]` request DTOs

## Domain Model

Financial management app with two user roles:
- **Administrator** — registers via email+password; manages categories and all users
- **User** — registers via Brazilian phone+password; creates and views own transactions

Core entities: `User` (extends `IdentityUser`) → `Transaction` → `Category` → `CategoryPurpose`. Lookup tables `TransactionType` and `CategoryPurpose` are seeded in the initial migration.

EF Core table naming: `tb_<entityname>` (e.g., `tb_user`, `tb_category`). Fluent API configurations are in `Data/Configurations/`.

## Authentication

JWT Bearer tokens (10-minute expiry) + refresh tokens backed by ASP.NET Core Identity token provider (database-stored).

- Web clients: refresh token is set as an `HttpOnly` cookie
- Mobile clients: refresh token is returned in the response body
- JWT claims: `uid` (user ID), `email` or `phone_number` (whichever the user registered with), roles

Token generation logic is in `Repositories/Repository/AccountRepository.cs`. Note: the JWT Bearer `OnChallenge` handler in `Program.cs` overrides the default response to return `403 Forbidden` (not the standard `401 Unauthorized`) for any missing/invalid token.

## Configuration

All settings live in `appsettings.json` / `appsettings.Development.json`. Key sections:

- `ConnectionStrings:DefaultConnection` — MySQL connection string (database: `FamilyVault`, default port 3306)
- `JwtSettings` — `Issuer`, `Audience`, `DurationInMinutes`, `Key`

CORS is configured to allow `http://localhost:5173` (expected frontend origin).

## Validators

`Common/Validators/` contains regex-based `EmailValidator` and Brazilian phone format `PhoneValidator`. Phone format is enforced via `PhoneValidatorCustomAttribute` on request DTOs.

## Nullable / Language Settings

Nullable reference types are enabled project-wide. Implicit usings are on — no need to add common `using` statements manually. Code comments and standard messages (`Common/StandardMessages.cs`) are in Portuguese.
