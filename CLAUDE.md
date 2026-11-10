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

# Integration tests (no database needed: in-memory SQLite)
dotnet test FamilyVaultApi.IntegrationTests
```

Swagger UI (Development only) is available at `http://localhost:5090/swagger` or `https://localhost:7072/swagger` when running.

`FamilyVaultApi.IntegrationTests` (xUnit + FluentAssertions + `Microsoft.AspNetCore.Mvc.Testing` + EF Core SQLite) boots the API through `FamilyVaultApiFactory` (`WebApplicationFactory<Program>`) in the `Testing` environment: `Program.cs` skips the MySQL `AutoDetect`/`UseMySql` registration there, and the factory registers an in-memory SQLite connection, runs `EnsureCreated` (roles come from `HasData`) before startup, because `AdminSeeder` runs right after `Build()`. `Data/AdminSeederTests.cs` creates one factory per test (own database and `AdminSeed` settings). The HTTP tests in `Http/` (auth and the 401/403 envelope, permissions, category and transaction CRUD with the owner rule) share one `ApiFixture` per class (`IClassFixture`): one factory with the seeded admin, cached tokens (login is rate limited to 5/min per API), two reusable logged-in Users (`UserClientAsync(0|1)`) and `NewUserAsync()` for a fresh user without login; each test creates its own records. Responses are read with `ReadEnvelopeAsync<T>()`. Coverage: `coverage.runsettings` (API project only, `Migrations/` excluded); CI merges both projects with ReportGenerator into the job summary.

`Family-Vault-Api.sln` has four projects: `FamilyVaultApi` (the API), `FamilyVaultApi.IntegrationTests` (above), `FamilyVaultApi.Evals` (console app that calls the real AI and costs money; run by hand or by the manual `Evals` workflow, never by `dotnet test`; see its README) and `FamilyVaultApi.UnitTests` (xUnit + Moq + FluentAssertions + Bogus, `net8.0`, `ProjectReference` to the main project). Tests live under `Services/` (one file per `Services/Service/*.cs` class), with mocks built per-test-class in the constructor (no shared fixtures) and fluent request builders in `Builders/<Entity>/` (`XxxDtoBuilder.New().With...().Build()`, defaults generated via `Bogus.Faker` rather than fixed constants). Naming convention: `MethodName_Scenario_ExpectedResult`. Unit tests cover the Service layer; Controllers and Repositories are covered through the HTTP integration tests (`FamilyVaultApi.IntegrationTests/Http/`).

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
- Global exception handling via `Middleware/ExceptionMiddleware.cs` — throw `BadRequestException` from Services/Repositories instead of returning error codes. It explicitly catches `BadRequestException` (400), `UnauthorizedAccessException` (401), `KeyNotFoundException` (404), `NotFoundException` (`Exceptions/NotFoundException.cs`, 404), `System.Security.SecurityException` (403), `SecurityTokenException` (401) and `DbUpdateException` (409, "Operação conflita com registros relacionados."). Anything else (e.g. `ArgumentException`, `InvalidOperationException`) falls through to the generic 500 handler on purpose — it means a programming error, so Services throw the project exceptions above for expected failures. Login answers a generic 401 ("Usuário ou senha inválidos.") for both unknown user and wrong password, so it doesn't reveal which accounts exist. Every response (including errors) uses the `ApiResponse<T>` envelope with a `TraceId` set from `HttpContext.TraceIdentifier`, and errors are logged with that same trace ID for correlation.
- **Categories have an owner**: `categories.user_id` null = global (created by an Administrator), otherwise personal. Administrators list only global categories; Users list global + their own. Editing/deleting a global category needs Administrator or `ManageCategories`; a personal one, its owner too (others get 404). A transaction only accepts a global category or one owned by the transaction's user. A category in use cannot be deleted or change purpose (`ConflictException` → 409).
- **Administrators**: the first one is seeded at startup from `AdminSeed:Email` / `AdminSeed:Password` (user-secrets; `Data/AdminSeeder.cs`) when none exists. `POST /api/register` is public and creates only Users, by phone or e-mail confirmed with a code (`POST /api/register/code`); `POST /api/User` (Administrator) creates Administrators or Users. The last Administrator cannot be deleted (409).
- Authorization mixes three mechanisms: role-based (`[Authorize(Roles = "...")]`), permission-based (`[Authorize(Policy = nameof(PermissionCode.X))]`, where `PermissionCode` is an enum in `Models/Internal/Enums/PermissionCode.cs` and claim types are in `AppClaimTypes.cs`), and resource-ownership checks done inside the Service layer rather than via `[Authorize]`. For actions like updating/deleting a User or Transaction, the controller only requires the `Administrator`/`User` role and passes the `ClaimsPrincipal` into the service; the service allows Administrators and permission-policy holders to act on any record, and otherwise throws `NotFoundException` (record doesn't exist) or `System.Security.SecurityException` (403, caller isn't the owner) — see `TransactionService.EnsureCanModifyTransactionAsync` and the equivalent ownership check in `UserService`. Listing follows the same rule: `TransactionService.GetAllAsync` receives the `ClaimsPrincipal` and, unless the caller is an Administrator or holds `ManageTransactions`, overrides `query.UserId` with the caller's `uid`. Transaction and Category listings normalize paging like `UserService` (`PageNumber` ≥ 1, `PageSize` 1–100, default 20). Permission claims are attached to roles and seeded via a dedicated migration (`SeedAdministratorPermissionClaims`).
- There is no OData nor API versioning: the unused `Microsoft.AspNetCore.OData`, `Asp.Versioning.*` and other dead packages were removed. Category and Transaction GET endpoints use hand-rolled `[FromQuery]` DTOs (`CategoryQueryRequestDto`, `TransactionQueryRequestDto`, extending `PagedFilterDto`) bound via normal model binding.
- **Errors outside the middleware also use the envelope**: the JWT `OnChallenge` (401 "Acesso negado."), `OnForbidden` (403) and the rate-limit 429 (`Middleware/ApiIpRateLimitMiddleware.cs`, subclass of `IpRateLimitMiddleware`, because the lib's `QuotaExceededResponse` is a fixed body with no `traceId`) are written by `Common/ApiResponseWriter.cs` as `ApiResponse<string>` with `TraceId`.
- Seeds use fixed dates (`SeedDate` in `Data/DatabaseContext.cs`) and fixed role `ConcurrencyStamp`s, so a new migration does not regenerate `UpdateData` for them.

## Domain Model

Financial management app with two user roles:
- **Administrator** — registers via email+password; manages categories and all users
- **User** — registers via Brazilian phone+password; creates and views own transactions

Core entities: `User` (extends `IdentityUser`) → `Transaction` → `Category` → `CategoryPurpose`. Lookup tables `TransactionType` and `CategoryPurpose`, plus initial `Category` rows, are seeded in the initial migration.

`DashboardController` (`GET /api/dashboard/summary`) aggregates transaction totals per `Category`/`TransactionType` for a date range (`StartDate`/`EndDate` on `DashboardQueryRequestDto`, filtered in `DashboardRepository`); results are scoped to the caller's own data when the caller is a `User`, and unscoped for `Administrator`. If neither `StartDate` nor `EndDate` is supplied, `DashboardService.GetSummaryAsync` defaults the range to the current UTC calendar month; supplying only one of the two leaves the other open-ended (no default applied).

EF Core table naming is mixed: Identity/user tables use a `tb_` prefix (`tb_user`, `tb_roles`, `tb_user_roles`, `tb_user_claims`, `tb_user_logins`, `tb_user_tokens`, `tb_role_claims`, configured in `Data/DatabaseContext.cs`), while domain entity tables use plain plural snake_case with **no** prefix (`categories`, `category_purposes`, `transactions`, `transaction_types`, configured via Fluent API in `Data/Configurations/`).

## Authentication

JWT Bearer tokens (10-minute expiry, `JwtSettings:DurationInMinutes`) + opaque refresh tokens stored as SHA-256 hashes in `tb_refresh_tokens` (`RefreshTokenService`, `RefreshTokenRepository`).

- **Lockout**: 5 wrong passwords lock the account for 15 minutes (Identity lockout); a locked account gets the same generic 401 as a wrong password, so login never reveals which accounts exist.
- **Refresh token lifetime**: 7 days, renewed on each rotation; the web cookie expires in 7 days. The cookie is `HttpOnly`, `Secure`, `SameSite=Lax` and `Path=/api`: it only works first-party, so the web panel must reach the API through its own origin (Vite dev proxy `/api` → the API), not cross-site.
- **Per-device sessions**: each login opens a new token family (`family_id`, the session) without touching the others, so web, app and several devices coexist. A token only works on its own client (`AuthClient`, from the route: `web/*` vs `app/*`).
- **Refresh validation** (`AccountService.RefreshTokenAsync`): the expired access token is validated by `ITokenService.GetPrincipalFromExpiredToken` (signature, issuer, audience and HmacSha256; lifetime ignored) and the user is loaded by its `uid`. `IRefreshTokenService.RotateAsync` then looks the refresh token up by hash; an unknown, expired or revoked token, or one from another user or client, answers 401 without revoking anything.
- **Rotation and reuse detection**: `TryConsumeAsync` marks the token as consumed with a conditional update, so only one concurrent request wins. A token reused up to 10 s after being consumed (race or lost response) gets a new token in the same family; after that, its family is revoked and the answer is 401. Other sessions are not affected.
- **Logout** (`POST /api/logout`, anonymous, `{refreshToken}` in the body or the web cookie) revokes only that token's family and always answers 200. Password changes and resets revoke every session through `IIdentityService.RevokeRefreshTokensAsync`, which also rotates the security stamp.

- **Passwords** (`Services/Service/PasswordService.cs`, `PasswordController` under `api/password`, plus `POST /api/User/{id}/password`):
  - `POST /api/register/code` (anonymous, `{phoneNumber}` or `{email}`, exactly one else 400) always answers 202; an existing account, malformed destination or exceeded limit gets no code. The 6-digit code (`RandomNumberGenerator`) lives in `IRegistrationCodeStore` (`DbRegistrationCodeStore`, table `tb_verification_codes`: keyed HMAC of the code, `expires_at`, atomic failure counter). `POST /api/register` (`RegisterAccountRequestDto`, `code` + phone or e-mail) verifies it after the password/destination validations and removes it only once the account is created; the destination becomes the `UserName` (E.164, or e-mail trimmed + lower-case) and is marked confirmed. A bad code -> 400 "Código inválido ou expirado.".
  - `POST /api/password/forgot` (anonymous, `{phone}` or `{email}`, exactly one else 400) always answers 202, even for unknown, locked or malformed destinations. For a valid account it generates a 6-digit code with the Identity token provider of the channel (`TokenOptions.DefaultPhoneProvider` for SMS, `DefaultEmailProvider` for e-mail, purpose `ResetPasswordCode`) and hands it to `IVerificationCodeSender`. By e-mail only an account whose `UserName` is that e-mail qualifies. The services receive `VerificationCodeQueue`, which only enqueues (so response time doesn't reveal which accounts exist); `VerificationCodeDispatcher` (`BackgroundService`) delivers through the sender registered under `VerificationCodeQueue.SmsDeliveryKey` or `EmailDeliveryKey`, by the message channel. Delivery is `TwilioVerificationCodeSender` (SMS) and `ResendVerificationCodeSender` (e-mail) outside Development/Testing (the API refuses to start without `Sms:Twilio:AccountSid`, `AuthToken`, `From` and `Email:Resend:ApiKey`, `From`, set as `Sms__Twilio__*` / `Email__Resend__*` variables) and `LoggingVerificationCodeSender` on both keys in Development/Testing (code in the log **only in Development**). Texts live in `VerificationCodeTexts`. A failed delivery is logged with a masked destination (`DestinationMask`) and the request still answered 202.
  - `POST /api/password/reset` (anonymous, `{phone}` or `{email}`, `code, password, passwordConfirm`): unknown/locked account or bad code -> 400 "Código inválido ou expirado." (a wrong code on an existing account calls `AccessFailedAsync`, so guessing the code locks the account). On success it uses `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` (password validators apply), unlocks the account and revokes the refresh tokens.
  - `POST /api/password/change` (Administrator/User, user from the `uid` claim) uses `ChangePasswordAsync` and revokes the refresh tokens. `POST /api/User/{id}/password` (Administrator) sets another user's password (404 if missing) and revokes their tokens.
  - `PortugueseIdentityErrorDescriber` (registered with `AddErrorDescriber`) translates only the Identity password-rule errors, `PasswordMismatch` and `InvalidToken`.
  - Rate limits: `post:/api/register/code` 3/min, `post:/api/password/forgot` 3/min, `post:/api/password/reset` 5/min, `post:/api/password/change` 5/min (per IP). Per destination (phone or e-mail), `register/code` and `forgot` share `IVerificationSendLimiter` (`DbVerificationSendLimiter`, table `tb_verification_send_limits`, atomic conditional updates, `VerificationCode:SendLimit`: 60 s between sends, 1 per 24 h window, SMS only to +55 and 5 SMS per day overall; values are deliberately low while the app is not launched), only for existing unlocked accounts (reset) or accounts that don't exist yet (sign-up); over the limit it answers 202 without sending.
- JWT claims: `uid` (user ID), `email` or `phone_number` (whichever the user registered with; a User registered by e-mail carries the e-mail in the `phone_number` claim), `SecurityStamp`, role claims, and permission claims (`AppClaimTypes.Permission`) pulled from the user's role via `RoleManager.GetClaimsAsync`.
- Web vs. mobile is selected by route, not by inspecting the request: `AccountController` exposes separate `web/login` + `web/refreshtoken` (refresh token set as an `HttpOnly` cookie, stripped from the response body) and `app/login` + `app/refreshtoken` (refresh token returned in the response body) endpoints. `LoginAsync` separately infers Administrator-vs-User login by which of `loginDto.Email`/`loginDto.Phone` is populated (exactly one must be set) — that check is about credential type, not client platform.

Access token generation and expired-token validation live in `Services/Service/TokenService.cs`; refresh token rules in `Services/Service/RefreshTokenService.cs`. Note: the JWT Bearer `OnChallenge` handler in `Program.cs` overrides the default response to return a `401` in the `ApiResponse<string>` envelope ("Acesso negado.") for any missing/invalid token.

## Configuration

Settings live in `appsettings.json` / `appsettings.Development.json`, except the JWT key. Key sections:

- `ConnectionStrings:DefaultConnection` — MySQL connection string (database: `FamilyVault`, default port 3306)
- `JwtSettings` — `Issuer`, `Audience`, `DurationInMinutes` in the appsettings; `Key` is **not versioned**: user-secrets in Development (`dotnet user-secrets set "JwtSettings:Key" "<32+ characters>"`), env var `JwtSettings__Key` elsewhere. Startup fails with a clear message if it is missing or under 32 bytes.

Swagger/SwaggerUI are mapped only in Development; `UseHsts` only outside Development.

CORS is configured (policy `"WebClient"`) to allow `http://localhost:5173` (expected frontend origin).

## Validators

`Common/Validators/` contains a regex-based `EmailValidator`. Phone numbers use a two-layer pattern: `PhoneValidatorCustomAttribute`/`PhoneValidatorCustomRequiredAttribute` (`Common/Validators/DtoValidators/`) do a cheap structural check on the DTO (digits/`+`/spaces/parentheses/hyphen, plausible length) with no DI, applied to `CreateAccountRequestDto.PhoneNumber`, and `LoginRequestDto.Phone` (the forgot/reset and `register/code` DTOs have no format validator, so malformed phones get the generic answer; `POST /api/register` uses `RegisterAccountRequestDto` with the non-required `PhoneValidatorCustom`); real per-country validation and E.164 normalization happen in `Services/Service/PhoneNumberService.cs` (`IPhoneNumberService`, wraps `libphonenumber-csharp`, registered as a DI singleton in `Program.cs`), called from `AccountService` during registration. Canonical stored phone format is E.164 with a leading `+` (e.g. `+5511987654312`) — `UserName`/`NormalizedUserName`/`PhoneNumber` on `User` must stay in sync with this format since login looks the phone up by exact string after normalizing it with `IPhoneNumberService` (an invalid phone gets the same generic 401).

## Nullable / Language Settings

Nullable reference types are enabled project-wide. Implicit usings are on — no need to add common `using` statements manually. Code comments and standard messages (`Common/StandardMessages.cs`) are in Portuguese.

## Notes on README.md

The repo has a `README.md` with a full endpoints table (Account, Category, Transaction, User, Dashboard routes with required roles and query filters) that matches the current controllers — keep it in sync when a route, its authorization or its filters change.

Serilog, health checks, and rate limiting (all listed as README features) **are** wired up in `Program.cs`: `UseSerilog`/`UseSerilogRequestLogging` write to console + a rolling file (`Serilog` section in `appsettings.json`), `AddHealthChecks().AddMySql(...)` backs `GET /health`, and `AddInMemoryRateLimiting`/`UseIpRateLimiting` (AspNetCoreRateLimit, `IpRateLimiting` section) throttles auth endpoints (3-10 req/min on login/register/password forgot+reset/refresh routes, 429 in the `ApiResponse<string>` envelope on breach, through `ApiIpRateLimitMiddleware`). Response caching is intentionally **off**: an inline middleware sets `Cache-Control: no-store` on every response, since almost every route is authenticated and per-user.

A GitHub Actions workflow (`.github/workflows/ci.yml`) runs `dotnet build` + `dotnet test` on `FamilyVaultApi.UnitTests` and `FamilyVaultApi.IntegrationTests` on every push/PR to `main`; there is no deploy step (portfolio project, not deployed anywhere).

## Charge (bills by upload)

`ChargeController` (`api/charge`, Administrator/User, always scoped to the caller's own charges, administrators included) turns an uploaded bill (PDF/JPEG/PNG, type read from the first bytes, hash in `inbound_documents.external_id`) into a `Charge` for review; only `POST {id}/confirm` creates a transaction (expense dated at the due date, through `ITransactionService`, in the same DB transaction that marks the charge `Confirmed` by a conditional UPDATE; 409 if it was already decided). `ChargeProcessingWorker` reads `Pending` documents from the database every `Charges:Worker:PollIntervalMilliseconds` and calls the scoped `ChargeDocumentProcessor` (reserve by conditional UPDATE, 3 attempts, stuck `Processing` goes back to `Pending`, `Charges:DailyLimits` 20 per user / 100 total per UTC day, the rest waits for the next day); it does nothing while no `IChargeExtractor` is registered: `AnthropicChargeExtractor` (official `Anthropic` SDK, PDF/image in, structured output, no tools, unreadable document or 400 from the API -> `ChargeExtractionException`) is registered only when `Ai:ApiKey` is set (user-secrets or `Ai__ApiKey`, never in a versioned file); model and effort in `Ai:Model` / `Ai:Effort`. Its unit tests run the real SDK against a fake `HttpMessageHandler`, and the integration tests replace it with `FakeChargeExtractor`, so the suite never calls the AI or when `Charges:Worker:Enabled` is false (the integration tests turn it off and call the processor directly). The AI only extracts: `ChargeValidator` checks barcode (check digits), amount and due date, and the processor checks the suggested category; a possible duplicate expense (same amount, due date within 3 days) is only flagged. `temporary_content` is cleared when the document ends; logs never carry content, barcode or extracted values. `TooManyRequestsException` is mapped to 429 by the `ExceptionMiddleware`.
