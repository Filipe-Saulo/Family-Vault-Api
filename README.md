# Family Vault API


**Monolithic .NET 8 Web API** for managing users, accounts, categories, and transactions.  
Supports **web and mobile clients**.


---


## Features


- ASP.NET Core 8 Web API (monolithic architecture)
- JWT authentication with ASP.NET Core Identity
- Role-based authorization (`Administrator`, `User`)
- Entity Framework Core with MySQL (Pomelo)
- AutoMapper for DTO mapping
- OData support for filtering, ordering, and selecting
- Serilog logging with console and Seq sinks
- Swagger / OpenAPI documentation
- Response caching
- Health checks for MySQL
- Rate limiting support
- Multi-platform: web apps and mobile apps


---


## Requirements


- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- MySQL 8.x
- Seq (optional for logging)


---


## Getting Started


1. Clone the repository:


```bash
git clone <repo-url>
cd FamilyVaultApi

Configure connection string and JWT settings in appsettings.json:

"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=FamilyVaultDb;User=root;Password=your_password;"
},
"JwtSettings": {
  "Issuer": "FamilyVaultApi",
  "Audience": "FamilyVaultApiClient",
  "Key": "YOUR_SECRET_KEY_HERE"
}

Apply database migrations:

dotnet ef database update

Run the API:

dotnet run

Access Swagger UI:

https://localhost:5001/swagger
Endpoints
Account
Method	Endpoint	Description
POST	/api/register	Register new account
POST	/api/login	Authenticate and return JWT
POST	/api/logout	Logout user
POST	/api/refreshtoken	Refresh access token using refresh token
POST	/api/resetPassword	Reset user password
Category
Method	Endpoint	Roles
POST	/api/category	Administrator
GET	/api/category	Administrator, User
DELETE	/api/category/{id}	Administrator
Transaction
Method	Endpoint	Roles
POST	/api/transaction	Administrator, User
GET	/api/transaction	Administrator, User
DELETE	/api/transaction/{id}	Administrator, User
User
Method	Endpoint	Roles
GET	/api/User	Administrator
DELETE	/api/User/{id}	Administrator

Technologies

.NET 8, C#

ASP.NET Core Web API

Entity Framework Core (Pomelo MySQL)

AutoMapper

Serilog

OData

Swagger / OpenAPI
