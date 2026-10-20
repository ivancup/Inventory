# Inventory Management Implementation Plan

This is the execution checklist for the .NET 10 inventory solution. It records implementation decisions and the failures encountered during the first execution so a repeat run can proceed in order with explicit readiness gates instead of rediscovering setup problems.

For the chronological record of user prompts, requirements, and settled project decisions, see [promps.md](promps.md).

## Confirmed scope

- Layered projects: `Inventory.Api`, `Inventory.Service`, `Inventory.Repository`, `Inventory.Domain`, `Inventory.Utils`, `Inventory.UnitTest`, and `Inventory.E2ETest`.
- Docker Compose runs the API, SQL Server, and Keycloak. SQL Server hosts separate `Inventory` and `AuditLogs` databases.
- Inventory tables are `Product`, `ProductCategory`, `InventoryMovement`, and `User`. A category has many products; a product has one category.
- Products are soft-deleted. A `Delete` movement has quantity zero, preserving on-hand quantity.
- Keycloak OIDC subjects identify users; passwords are not stored in the inventory database.
- Audit rows include change type, entity and ID, old/new JSON snapshots, actor subject, and UTC timestamp.
- EF Core migrations own both schemas; EF Core handles reads; parameterized Dapper commands handle writes.
- Inventory changes, movement rows, actor creation when needed, and audit rows commit in one SQL Server transaction across the two databases. Both databases must be on the same SQL Server instance; do not split the audit write to a separate server without redesigning atomicity.
- Source code follows one class/record per file. Keep dependency versions in `Directory.Packages.props`.
- Local settings are JSON plus Compose environment overrides. Azure App Configuration is not part of this implementation.
- Local dev defaults are intentionally static for convenience: SQL `sa` / `InventoryLocal_2026!`; Keycloak admin `local-admin` / `InventoryLocal_2026!`. They must never be reused outside an isolated local environment.
- Imported `inventory` realm includes the API test account `inventory-user` / `InventoryUser_2026!`; `Inventory.Api\Inventory.http` uses Visual Studio named-request variables to acquire and reuse its access token. Use `127.0.0.1:8080` from the host-side file rather than Docker's `keycloak` service DNS name or ambiguous `localhost` resolution. This is separate from the `master` realm admin login.

## Execution status

The implementation tasks below were completed. The latest E2E validation passed all 4 tests with none skipped using a freshly acquired local Keycloak token and host SQL connection. Reusing an expired token caused a 401 on product creation; obtain a new token immediately before each test run. The regular Compose-launched API, SQL Server, and Keycloak were healthy; both EF migrations were present in their respective databases. The E2E host URL defaults to `http://127.0.0.1:8081`; Visual Studio Fast mode is not a suitable test target while it is waiting for a debugger to start the API process. Re-run the full validation procedure below if rebuilding or changing the stack.

The local SQL Server `sa` password and Keycloak `local-admin` password were subsequently set to the fixed development value documented in `Inventory.Api/appsettings.Local.example.json`. Existing local SQL and Keycloak volumes were rotated in place and verified; the stack remains healthy. These credentials are strictly for isolated local development.

The Docker Compose files now also provide those local-only defaults directly during Compose variable interpolation. This is required for Visual Studio's Docker Compose profile, which invokes `docker compose config` without running the PowerShell launcher first. The exact Visual Studio config invocation was verified with the credential environment variables unset.

## Ordered tasks and completion gates

1. **Design documentation** — Maintain `DESIGN.md` with actual project dependencies, schemas, auth issuer, Compose addresses, migration behavior, cross-database transaction boundary, and tested local workflow. Do not document `.env` when the implementation uses `Inventory.Api/appsettings.Local.json`.
2. **README and local configuration** — Document exact Windows PowerShell setup, Keycloak hostname mapping, credentials, startup/readiness, migration checks, routes, and E2E prerequisites. Distinguish smoke-only tests from the authenticated test path.
3. **Compose infrastructure** — Provide SQL Server, Keycloak, and API with persistent data and health checks. Build from the solution root, copying every project manifest and `Directory.Packages.props`; verify the Dockerfile does not point at the removed starter `Inventory/Inventory.csproj`.
4. **Code-first schema and seed** — Create separate EF contexts, design-time factories, and migrations. Include the Product quantity check, movement quantity/type checks, foreign keys, seeds, and soft-delete defaults. Add one migration per context and apply both with `Database.MigrateAsync`; do not use `EnsureCreated` alongside migrations.
5. **Repositories** — Use EF Core for query paths and parameterized Dapper for command paths. Require the same SQL Server connection for `Inventory` and `AuditLogs` so one local SQL transaction enlists both databases. Keep audit insertion inside the write transaction and prove failure rolls back stock, movement, and audit rows.
6. **Services/CQRS** — Validate positive movement quantities, available stock, quantity overflow, valid actor subject, and page bounds before writes. Keep service interfaces and implementation in separate files. Preserve the Keycloak `sub` claim.
7. **Versioned APIs** — Verify route version prefixes and auth policies. Test unauthenticated mutation returns 401, authenticated mutations pass, and audit paging is stable.
8. **Logging and metrics** — Use source-generated structured log methods and OpenTelemetry counters tagged with product/category IDs. Avoid credentials, tokens, and personal secrets in logs.
9. **Tests and live run** — Run unit tests; start Compose; wait until all services are healthy; confirm API `/health` and read endpoints; verify both migrations; then run host SQL constraint and authenticated mutation/audit/rollback tests. Do not count an E2E run that skips the authenticated flow as complete.

## Lessons from the first execution

- **Compose is not ready just because containers started.** Wait for all three services to report healthy, call `http://localhost:8081/health`, and verify a read endpoint before running E2E tests. The first API request timed out while API startup/migrations were still in progress.
- **Host and container addresses differ.** The API uses `sqlserver` and `keycloak` on the Compose network. On Windows, host tools connect to SQL Server at `127.0.0.1,1433` (not `localhost` in this environment). Keycloak publishes on port 8080.
- **OIDC issuer must be byte-for-byte consistent.** Keycloak tokens identify issuer as `http://keycloak:8080/realms/inventory`; configure the API to use that exact authority. For host-browser/token access, add `127.0.0.1 keycloak` to `C:\Windows\System32\drivers\etc\hosts`. Do not configure the API to validate a different `localhost` issuer.
- **Keep raw JWT claims.** Set `JwtBearerOptions.MapInboundClaims = false`; otherwise the `sub` subject is mapped and actor resolution fails with a 401 on protected writes even though a token can authenticate successfully on another route.
- **Create a fully configured Keycloak test user.** For direct local token grant, set the user enabled, email verified, first and last name populated, a non-temporary password, and no pending required actions. Otherwise Keycloak can return `invalid_grant` (“Account is not fully set up”).
- **Run authenticated tests with a real token.** `INVENTORY_ACCESS_TOKEN` absent means the mutation E2E is inconclusive/skipped; get a token from the imported `inventory` realm before declaring full E2E success.
- **Refresh E2E tokens every run.** Keycloak access tokens expire quickly. A stale token returns 401 on the first protected write; obtain a new token and set `INVENTORY_ACCESS_TOKEN` in the same PowerShell process that starts `dotnet test`.
- **Host SQL test connection uses IPv4.** Set `INVENTORY_DB_CONNECTION` to the local SQL connection using `Server=127.0.0.1,1433`. The API connection string inside Compose must instead use `Server=sqlserver,1433`.
- **Use MSTest's actual assertion API.** With `MSTest.Sdk/4.2.3`, asynchronous exception assertions use `Assert.ThrowsAsync<T>`, not `ThrowsExceptionAsync`.
- **EF tooling needs design packages in both projects.** The startup API and repository need `Microsoft.EntityFrameworkCore.Design`; keep the version centrally managed and install a matching `dotnet-ef` tool version. Generate migrations separately with `--context InventoryDbContext` and `--context AuditDbContext`.
- **Local secrets must not enter Docker build context or source control.** Ignore `Inventory.Api/appsettings.Local.json` in Git and `.dockerignore`; use the checked-in example only as a template, and use the launcher to pass secrets/settings to Compose.
- **Compose credentials only bootstrap new persistent volumes.** Changing the configured static password does not rotate credentials in an already initialized SQL Server or Keycloak volume. Rotate in place, or explicitly discard local data with `.\scripts\start-local.ps1 -Down -Volumes` before reinitializing.
- **Keep documentation aligned with runnable commands.** Use `.\scripts\start-local.ps1 -Detached` and `-Down` / `-Volumes`; Compose arguments such as `-d` are not passed through arbitrary script arguments. A script's temporary environment variables do not persist after it exits, so explicitly set host E2E variables in the test shell.
- **Visual Studio Compose does not inherit launcher-shell variables.** Use Compose interpolation defaults for the documented local-only SQL and Keycloak credentials; otherwise VS fails during `docker compose config` before containers start.
- **Run only one Compose project at a time.** The launcher and Visual Studio use different project names but publish the same host ports (`8080`, `8081`, `1433`). Stop the active stack without `-Volumes` before switching launch methods; this preserves the database and Keycloak volumes.
- **Use the Visual Studio `docker-compose` startup project for Compose networking.** The API's standalone `Container (Dockerfile)` profile starts an isolated container and cannot resolve `sqlserver`; if running the API on the host, use `127.0.0.1,1433` instead.
- **Select only `docker-compose` as Visual Studio's startup project.** A concurrently selected `Inventory.Api` Dockerfile profile starts a separate `Inventory.Api` container on Docker's default `bridge`; its `appsettings.Local.json` points at `localhost,1433`, which is the API container itself. The Compose service has the right `sqlserver` hostname and environment overrides; verify the debug API container is the Compose-managed service on the same network as SQL Server.
- **Visual Studio debug waits to launch the API process inside its container.** The debug container initially runs `DistrolessHelper --wait`, so the API's `/health` probe gets connection refused until Visual Studio launches the debuggee. Keep the API healthcheck for regular Compose runs, but disable it in `docker-compose.vs.debug.yml`; continue checking SQL Server and Keycloak health, and verify API `/health` after debuggee launch.
- **Use regular Compose for host-run E2E tests.** Visual Studio Fast mode can leave the API container waiting for the debugger, causing host test requests to be aborted. The E2E client defaults to `http://127.0.0.1:8081`; bring up the regular stack and verify `/health` before running tests.

## Repeatable verification procedure (Windows PowerShell)

1. Create `Inventory.Api\appsettings.Local.json` by copying `Inventory.Api\appsettings.Local.example.json`; set strong local-only SQL and Keycloak passwords. Add `127.0.0.1 keycloak` to the Windows hosts file.
2. Run `.\scripts\start-local.ps1 -Detached`. Confirm `docker compose ps` reports SQL Server, Keycloak, and API healthy. Confirm `Invoke-RestMethod http://localhost:8081/health` succeeds, then check `http://localhost:8081/api/v1/categories`. In Visual Studio debug mode, SQL Server and Keycloak should be healthy; the API check is disabled until Visual Studio starts its debuggee, so verify API `/health` and the read endpoint directly instead.
3. Verify databases and migration history inside SQL Server:

   ```powershell
   docker exec inventory-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $env:MSSQL_SA_PASSWORD -C -Q "SELECT name FROM sys.databases WHERE name IN ('Inventory','AuditLogs'); USE Inventory; SELECT MigrationId FROM dbo.__EFMigrationsHistory; USE AuditLogs; SELECT MigrationId FROM dbo.__EFMigrationsHistory;"
   ```

4. Run `dotnet test Inventory.UnitTest\Inventory.UnitTest.csproj`.
5. Before full E2E, request a Keycloak token for an enabled, verified, fully configured realm user. Set `INVENTORY_ACCESS_TOKEN` to its access token and `INVENTORY_DB_CONNECTION` to the host connection string (`Server=127.0.0.1,1433`, the same SQL password and `Database=Inventory`). Run `dotnet test Inventory.E2ETest\Inventory.E2ETest.csproj`; require all tests to pass with none skipped.
6. The rollback test temporarily creates a rejecting trigger in local `AuditLogs`, sends a stock command, checks stock/movement/audit remain unchanged, and drops the trigger in `finally`. Run it only against the disposable local Compose databases with an SA-capable connection.
7. Stop services with `.\scripts\start-local.ps1 -Down`; add `-Volumes` only to intentionally discard local SQL and Keycloak data.

## Repository/worktree safety

The starting worktree already had uncommitted project additions, removals, and modifications. Preserve existing user changes; inspect `git status` before edits and never use destructive reset/checkout commands. Generated migration files may contain partial class declarations and EF-generated structure; keep the handwritten application classes in individual files.
