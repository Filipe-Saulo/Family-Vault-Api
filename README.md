# Family Vault API

**Monolithic .NET 8 API** for managing users, accounts, categories, and transactions.
Supports **web and mobile clients**.

---

## Features

- ASP.NET Core 8 Web API (monolithic architecture)
- JWT authentication with ASP.NET Core Identity
- Role-based authorization (`Administrator`, `User`) plus permission-claim policies (`ManageCategories`, `ManageTransactions`, `ManageTransactionTypes`, `ManageUsers`)
- Entity Framework Core with MySQL (Pomelo)
- AutoMapper for DTO mapping
- Serilog logging (console sink + request logging)
- Swagger / OpenAPI documentation
- Response caching
- Health check for MySQL at `/health`
- Multi-platform: web apps and mobile apps

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- MySQL 8.x

## Getting Started

1. Clone the repository:

```bash
git clone <repo-url>
cd FamilyVaultApi
```

2. Configure connection string and JWT settings in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=FamilyVault;User=root;Password=your_password;"
},
"JwtSettings": {
  "Issuer": "FamilyVaultApi",
  "Audience": "FamilyVaultApiClient",
  "DurationInMinutes": 10,
  "Key": "YOUR_SECRET_KEY_HERE"
}
```

3. Apply database migrations:

```bash
dotnet ef database update
```

4. Run the API:

```bash
dotnet run --project FamilyVaultApi
```

5. Access Swagger UI:

- `http://localhost:5090/swagger`
- `https://localhost:7072/swagger`

## Endpoints

### Account

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/register` | Register new account (Administrator via email, User via phone) |
| POST | `/api/web/login` | Authenticate; refresh token set as an HttpOnly cookie |
| POST | `/api/app/login` | Authenticate; refresh token returned in the response body |
| POST | `/api/logout` | Logout user (clears the refresh token cookie if present, no-op otherwise) |
| POST | `/api/web/refreshtoken` | Refresh access token; reads/writes the refresh token via cookie |
| POST | `/api/app/refreshtoken` | Refresh access token; reads/writes the refresh token via request/response body |
| POST | `/api/resetPassword` | Reset user password |

### Category

| Method | Endpoint | Roles |
|---|---|---|
| POST | `/api/category` | Administrator, User |
| GET | `/api/category` | Administrator, User |
| PUT | `/api/category/{id}` | `ManageCategories` permission |
| DELETE | `/api/category/{id}` | `ManageCategories` permission |

### Transaction

| Method | Endpoint | Roles |
|---|---|---|
| POST | `/api/transaction` | Administrator, User |
| GET | `/api/transaction` | Administrator, User |
| PUT | `/api/transaction/{id}` | `ManageTransactions` permission |
| DELETE | `/api/transaction/{id}` | `ManageTransactions` permission |

### TransactionType / CategoryPurpose (lookups)

| Method | Endpoint | Roles |
|---|---|---|
| GET | `/api/transactiontype` | Administrator, User |
| POST / PUT | `/api/transactiontype` | `ManageTransactionTypes` permission |
| GET | `/api/categorypurpose` | Administrator, User |
| POST / PUT | `/api/categorypurpose` | `ManageCategories` permission |

### User

| Method | Endpoint | Roles |
|---|---|---|
| GET | `/api/User` | `ManageUsers` permission |
| PUT | `/api/User/{id}` | Administrator, or the user themselves |
| DELETE | `/api/User/{id}` | Administrator, or the user themselves |
| GET | `/api/User/{userId}/permissions` | Administrator |
| POST | `/api/User/{userId}/permissions` | Administrator |
| DELETE | `/api/User/{userId}/permissions/{permission}` | Administrator |

### Dashboard

| Method | Endpoint | Roles |
|---|---|---|
| GET | `/api/dashboard/summary` | Administrator, User (scoped to own data for `User`) |

### Health

| Method | Endpoint | Description |
|---|---|---|
| GET | `/health` | MySQL connectivity check |

## Technologies

- .NET 8, C#
- ASP.NET Core Web API
- Entity Framework Core (Pomelo MySQL)
- AutoMapper
- Serilog
- Swagger / OpenAPI
