# Inventory Management

.NET 10 layered inventory API using SQL Server, local Keycloak OIDC, EF Core migrations/queries, and Dapper transactions for writes.

## Architecture

See [DESIGN.md](DESIGN.md) for the project and database diagrams, auth issuer, cross-database transaction boundary, local network addresses, and failure-aware test flow.

- `Inventory.Api` — versioned HTTP endpoints, Keycloak bearer authentication, error handling, dependency injection, and OpenTelemetry.
- `Inventory.Service` — CQRS-facing operations, validation, and business rules.
- `Inventory.Repository` — EF Core contexts and migrations, read queries, and parameterized Dapper writes.
- `Inventory.Domain` — API models and shared domain concepts.
- `Inventory.Utils` — shared utilities.
- `Inventory.UnitTest` and `Inventory.E2ETest` — service/API unit tests and Compose-backed integration tests.

Keep each handwritten class or record in a separate source file. Keep package versions centralized in `Directory.Packages.props`.

## Prerequisites and first start

- .NET 10 SDK and Docker Desktop with Compose
- Windows hosts-file entry `127.0.0.1 keycloak` in `C:\Windows\System32\drivers\etc\hosts` (required so host-side Keycloak URLs and token issuer match)
- Local settings file created from the example:

```powershell
Copy-Item Inventory.Api\appsettings.Local.example.json Inventory.Api\appsettings.Local.json
```

For local development, the checked-in example configures SQL Server as `sa` / `InventoryLocal_2026!` and the Keycloak admin console as `local-admin` / `InventoryLocal_2026!`. These fixed credentials are intentionally convenient for this local-only project; never use them in a shared or production environment. The copied settings file is ignored by Git and excluded from the Docker image.

The default credentials are applied when the named Compose volumes are initialized. If you already have persistent volumes created with different credentials, either change those credentials in place or, only if you are willing to delete local database/Keycloak data, run `.\scripts\start-local.ps1 -Down -Volumes` before starting again.

Start in the repository root:

```powershell
.\scripts\start-local.ps1 -Detached
docker compose ps
Invoke-RestMethod http://localhost:8081/health
```

For the script/regular Compose launch, continue only when `inventory`, `sqlserver`, and `keycloak` all show `healthy` and the health request succeeds. All API endpoints other than `/health` require a bearer token; use the authenticated requests in `Inventory.Api\Inventory.http` to check read routes. In Visual Studio debug mode, its generated container waits for Visual Studio to launch the API process, so the API's Compose healthcheck is disabled by `docker-compose.vs.debug.yml`; wait for SQL Server and Keycloak to show `healthy`, then verify `http://localhost:8081/health` and an authenticated read endpoint. Containers showing `running` alone is not readiness.

Visual Studio can also start the solution's **Docker Compose** profile directly: Compose supplies the same fixed local-only defaults when no shell environment variables are set, so Visual Studio does not need to run `scripts/start-local.ps1` first. Set `docker-compose` as the only startup project; do not also start `Inventory.Api` with its Dockerfile profile. The script remains useful for explicit CLI startup and shutdown.

Run only one Compose stack at a time. The PowerShell launcher and Visual Studio use different Compose project names but publish the same host ports (`8080`, `8081`, and `1433`), so stop the launcher stack with `.\scripts\start-local.ps1 -Down` before starting the Visual Studio profile, and stop the Visual Studio stack before restarting through the launcher.

In Visual Studio, set the solution startup project to **docker-compose** and ensure it is the only selected startup project (right-click the solution, **Properties > Startup Project**). Do not also start `Inventory.Api` with its **Container (Dockerfile)** profile while relying on the Compose SQL Server: that profile launches an isolated container, not attached to Compose's network, so the Compose-only hostname `sqlserver` cannot resolve there. The standalone profile also reads `Inventory.Api\appsettings.Local.json`, whose `Server=localhost,1433` points back to that API container, not SQL Server. For a host-run API instead, use a host connection string such as `Server=127.0.0.1,1433` and ensure SQL Server is running.

To diagnose which mode Visual Studio actually launched, inspect `docker ps`: the API container should have a Compose-generated name such as `<project>-inventory-1` and share a Docker network with `sqlserver`. A separate `Inventory.Api` container on the default `bridge` network indicates the standalone Dockerfile profile was also launched; stop debugging and remove it from the solution's startup-project selection.

Addresses differ by network:

| Caller | API | Keycloak | SQL Server |
| --- | --- | --- | --- |
| Host machine | `http://localhost:8081` | `http://localhost:8080` for direct requests; `http://keycloak:8080` for OIDC issuer redirects (hosts-file entry above) | `127.0.0.1,1433` |
| Compose container | `http://inventory:8080` | `http://keycloak:8080` | `sqlserver,1433` |

The configured OIDC authority and token issuer must both remain exactly `http://keycloak:8080/realms/inventory`. Do not substitute `localhost` in the API authority.

## Database and migrations

The SQL Server instance contains `Inventory` and `AuditLogs`. In Development, API startup applies `InventoryDbContext` and `AuditDbContext` EF migrations and seed data. Confirm the migrations before E2E work:

```powershell
$local = Get-Content Inventory.Api\appsettings.Local.json -Raw | ConvertFrom-Json
$env:MSSQL_SA_PASSWORD = $local.LocalServices.MssqlSaPassword
docker exec inventory-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $env:MSSQL_SA_PASSWORD -C -Q "SELECT name FROM sys.databases WHERE name IN ('Inventory','AuditLogs'); USE Inventory; SELECT MigrationId FROM dbo.__EFMigrationsHistory; USE AuditLogs; SELECT MigrationId FROM dbo.__EFMigrationsHistory;"
```

For a future schema change, install a matching EF tool once, then generate a migration for each changed context:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.13
dotnet ef migrations add AddInventoryChange --project Inventory.Repository\Inventory.Repository.csproj --startup-project Inventory.Api\Inventory.Api.csproj --context InventoryDbContext --output-dir Migrations\Inventory
dotnet ef migrations add AddAuditChange --project Inventory.Repository\Inventory.Repository.csproj --startup-project Inventory.Api\Inventory.Api.csproj --context AuditDbContext --output-dir Migrations\AuditLogs
```

Keep `Microsoft.EntityFrameworkCore.Design` referenced by both the API startup project and Repository, with its version managed centrally. Use migrations, not `EnsureCreated`, alongside this migration lifecycle.

## Keycloak and API routes

Compose imports realm `inventory`, client `inventory-api`, and the local API test user `inventory-user` / `InventoryUser_2026!`. The Keycloak admin login (`local-admin`) is a separate account in the `master` realm and cannot be substituted in the inventory realm token request. These fixed credentials are for isolated local development only. Every API endpoint requires a bearer token from an authenticated user with a `sub` claim, except `GET /health`. A fallback authorization policy protects endpoints even when they do not have explicit authorization attributes. The application preserves the raw JWT `sub` claim; on the first write it associates that subject, name, and email with a `User` row. It does not store passwords.

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| GET | `/health` | Anonymous | Service health status. |
| GET | `/api/v1/products` | Bearer token | List products; supports `includeDeleted=true`. |
| GET | `/api/v1/products/{id}` | Bearer token | Retrieve a product by ID. |
| POST | `/api/v1/products` | Bearer token | Create a product. |
| PUT | `/api/v1/products/{id}` | Bearer token | Update a product's name and category. |
| DELETE | `/api/v1/products/{id}` | Bearer token | Soft-delete a product; records a zero-quantity movement and preserves stock. |
| GET | `/api/v1/categories` | Bearer token | List categories. |
| POST | `/api/v1/categories` | Bearer token | Create a category. |
| PUT | `/api/v1/categories/{id}` | Bearer token | Update a category name. |
| DELETE | `/api/v1/categories/{id}` | Bearer token | Delete a category only when no products reference it. |
| GET | `/api/v1/movements` | Bearer token | List movements with optional `productId`, `page`, and `pageSize`. |
| POST | `/api/v1/movements/add` | Bearer token | Add stock to a product. |
| POST | `/api/v1/movements/remove` | Bearer token | Remove stock from a product. |
| GET | `/api/v1/audit-logs` | Bearer token | Retrieve paginated audit records with `page` and `pageSize`. |
| GET | `/openapi/v1.json` (Development only) | Bearer token | OpenAPI document. |

OpenAPI is available in Development at `/openapi/v1.json`. Add/remove writes, movement rows, and corresponding audit JSON are committed in one transaction. `Inventory` and `AuditLogs` must remain on the same SQL Server instance for this transaction boundary.

`Inventory.Api\Inventory.http` contains a Visual Studio-compatible request sequence for health, token acquisition, and every API endpoint. It calls the API through `http://127.0.0.1:8081` and Keycloak through `http://127.0.0.1:8080`; both are published host ports, not Compose-only service names. Using explicit IPv4 loopback addresses avoids `localhost` IPv4/IPv6 or proxy-resolution differences in HTTP clients. Run **Get Keycloak access token** first; later requests read the token from the named response using Visual Studio's `{{login.response.body.$.access_token}}` syntax. Run **Create category** before **Create product**; run **Soft-delete the created product** before **Delete category**. Browser REST requests to `localhost` do not require the `keycloak` hosts-file entry; that mapping is only needed when a host-side OIDC flow follows Keycloak's configured issuer hostname.

## Validation and tests

Run unit tests:

```powershell
dotnet test Inventory.UnitTest\Inventory.UnitTest.csproj
```

The E2E suite has environment-dependent checks: without `INVENTORY_ACCESS_TOKEN`, the authenticated inventory flow is inconclusive; without `INVENTORY_DB_CONNECTION`, the direct SQL constraint check is inconclusive. Access tokens are short-lived: a stale token returns `401 Unauthorized`, so obtain a fresh token immediately before each run.

With the regular Compose services healthy, obtain a fresh token and set both variables in the same PowerShell window before running tests:

```powershell
$token = Invoke-RestMethod -Method Post `
  -Uri 'http://127.0.0.1:8080/realms/inventory/protocol/openid-connect/token' `
  -ContentType 'application/x-www-form-urlencoded' `
  -Body @{
    grant_type = 'password'
    client_id = 'inventory-api'
    username = 'inventory-user'
    password = 'InventoryUser_2026!'
  }

$local = Get-Content Inventory.Api\appsettings.Local.json -Raw | ConvertFrom-Json
$env:INVENTORY_DB_CONNECTION = ([string]$local.ConnectionStrings.Inventory -replace '(?i)Server=localhost', 'Server=127.0.0.1') -replace '(?i)Password=[^;]*', "Password=$($local.LocalServices.MssqlSaPassword)"
$env:INVENTORY_ACCESS_TOKEN = $token.access_token
dotnet test Inventory.E2ETest\Inventory.E2ETest.csproj
Remove-Item Env:INVENTORY_ACCESS_TOKEN,Env:INVENTORY_DB_CONNECTION
```

The test project defaults to `http://127.0.0.1:8081`; override it with `INVENTORY_API_BASE_URL` if the published API uses another host address. Run tests against the regular Compose-launched API, not while Visual Studio Fast mode is waiting for the debugger to start the application process. Require all E2E tests to pass with none skipped. The suite covers auth requirements, product/category updates, stock checks, audit records, soft deletion, SQL quantity constraint, and audit-failure rollback. The rollback test creates a temporary trigger in `AuditLogs`, forces an audit write failure, verifies no stock/movement/audit change survives, and removes the trigger in `finally`; run it only against disposable local databases with the local SA credentials.

Stop services using the same local settings file:

```powershell
.\scripts\start-local.ps1 -Down
```

Add `-Volumes` only when deliberately deleting local SQL Server and Keycloak data. The launcher's environment variables are temporary to that PowerShell process and are not inherited by a later shell; set E2E variables explicitly as shown.
