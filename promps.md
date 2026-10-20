# Project Prompts and Decisions

This document records the user's requests and the requirements and decisions that shaped the Inventory application, in the order they arose. It summarizes the conversation rather than reproducing spelling errors or implementation chatter. Runtime findings and fixes are identified separately from user-selected requirements.

## 1. Initial project request

The user asked to plan and build a local inventory-management application using .NET 10 and Docker Compose.

### Product and database requirements

- Use SQL Server in Docker, with two databases: `Inventory` and `AuditLogs`.
- In `Inventory`, include `Product`, `ProductCategory`, `InventoryMovement`, and `User`.
- Categories and users have an ID and name. Users also have email and OAuth subject ID; do not store passwords because authentication uses OAuth.
- Products have an ID, name, category foreign key, quantity, and soft-delete flag.
- Inventory movements record product, movement type (`add`, `remove`, or `delete`), quantity, and the user who made the change.
- Enforce that product and movement quantities cannot be negative.
- Keep an audit table in `AuditLogs` with change type and JSON representations of old and new values, populated according to each operation.
- Seed initial product and inventory data.

### Architecture and behavior requirements

- Divide the solution into `Inventory.Api`, Service, Repository, Utils, and Domain projects.
- Keep business logic in Service; database operations in Repository; shared utilities in Utils; API models, constants, and service models in Domain.
- Use interfaces and dependency injection for repository and service implementations.
- Use code-first Entity Framework definitions and migrations.
- Use CQRS: Entity Framework for queries and Dapper for transactional writes.
- Use transactions for inventory updates to preserve ACID behavior.
- Provide versioned product APIs for products, movements, product updates, and category updates; provide a paginated GET API for audit logs.
- Audit mutations to inventory data.
- Add OpenTelemetry metrics for inventory additions/removals, including product and category dimensions.
- Use source-generated logging and appropriate error/warning messages. Handle unexpected errors and return appropriate API errors.
- Create unit tests for services and APIs, plus end-to-end tests that exercise the API and persistence behavior.
- Maintain a plan, design document with Mermaid diagrams, and README. Work in the requested order: design, README, database/schema/seed, repositories, service/CQRS, APIs, logging/metrics, unit tests, then E2E tests.
- Use Central Package Management in `Directory.Packages.props` for any added package versions.

### Settled design details

- A product belongs to one category; a category can contain multiple products.
- Soft deletion retains the product's on-hand quantity and records a `Delete` movement with quantity zero.
- Inventory and audit databases stay on the same SQL Server instance so an inventory write, movement, and audit row can participate in one local transaction.
- The design uses EF Core migrations, not `EnsureCreated`, and preserves the OAuth `sub` claim for actor identification.

## 2. Plan and implementation requests

1. **Put the plan at the repository root.** The user requested copying the plan into the project root.
2. **Execute the plan.** The user then asked to implement the application, not only leave a proposal.
3. **Improve repeatability.** The user asked to revise the plan, design document, and README to record problems encountered and avoid repeating them.

## 3. Local API workflow and credentials

1. **Complete `Inventory.Api\Inventory.http`.** Include all API calls, request a Keycloak token, and use that token as a bearer token for protected APIs.
2. **Use fixed local credentials.** The user requested static SQL Server and Keycloak credentials for local development.
3. **Keep the HTTP file credentials aligned.** The user explicitly asked to update the `.http` requests to use the configured credentials.
4. **Move repository registration to the API.** The user requested moving `InventoryRepositoryRegistration` into the API project.
5. **Recreate the app's containers from scratch.** The user wanted a clean run to verify the app starts correctly. Existing volumes were not implicitly deleted; deleting volumes is a separate, data-destructive choice.

The resulting local-only credentials are SQL Server `sa` / `InventoryLocal_2026!`, Keycloak admin `local-admin` / `InventoryLocal_2026!`, and the imported API test user `inventory-user` / `InventoryUser_2026!`. These are for isolated local development only.

## 4. Visual Studio and local runtime troubleshooting

1. **Fix the first Visual Studio Compose error.** Visual Studio could not find Compose environment variables loaded only by the PowerShell launcher. Compose now has local-only interpolation defaults, so its configuration can resolve without that launcher.
2. **Fix a host-port conflict.** The launcher and Visual Studio use distinct Compose project names but publish the same ports. The operating rule is to run one stack at a time; stop a stack without deleting volumes before switching launch modes.
3. **Investigate SQL connection failures.** The user clarified that the Visual Studio Docker Compose option was selected. Runtime inspection found Visual Studio Fast mode's `DistrolessHelper --wait` in the API container before the app debuggee starts. This is normal debugger infrastructure and should not be removed manually. The API healthcheck is disabled only for Visual Studio debug mode; regular Compose retains it.
4. **Check `appsettings.Development.json`.** It only selects Development logging and startup migrations. The Compose environment supplies SQL connection strings and Keycloak settings at runtime, so the file is intentionally partial and is not the source of missing Compose credentials.
5. **Fix the HTTP-file host URLs and response variables.** The Visual Studio HTTP editor runs on the host, so its requests use published loopback ports (`127.0.0.1:8081` for the API and `127.0.0.1:8080` for Keycloak), not Compose DNS names. The file uses Visual Studio's named-request response expressions for the access token and created product ID.
6. **Run E2E outside Visual Studio Fast mode.** The user agreed E2E tests could run through PowerShell. Use the regular Compose-launched API, verify `/health`, and run tests in a PowerShell process containing a fresh Keycloak access token and a host SQL connection string.

## 5. Validation outcome

- The `.http` sequence was verified with successful token acquisition and protected API requests.
- The first E2E run against Visual Studio Fast mode failed because its API container was waiting for the debugger, not serving requests.
- A separate E2E attempt received `401 Unauthorized` when using a stale token. Refreshing the token immediately before testing resolved it.
- The regular Compose E2E run passed all four tests, with zero failures and zero skips. The E2E host URL defaults to `http://127.0.0.1:8081`.
- The preview .NET SDK notice (`NETSDK1057`) is informational and was not the cause of the E2E failures.

## 6. Initial prompts
Ok let's plan the following c# project, the prject is for inventory ,management, is develop using dotnet 10
using docker compose, we need a dataabse sql server, preferibly using docker image to cvonnect to the database, we need 2 databases (Inventory, AuditLogs)
Inventory Databse, requires for now 2 tables Product and ProductCategory, the ProductCategory and InventoryMovement, Users, table will have an Id and a Name.
The Product table will have and Id, a Name, a foreign key to product category, let's keep it simple with a 1:many relationship, and Quantity for the inventory, and a IsDeleted flag
we will apply a softdelete, the next table is InventoryMovement that will have Id, ProductId foreigh key for Product table, and type of movement(add, remove or delete), and quantities
and UserId to identity who appklyed the change this will be a foreign key pointing to the User table
The User table will have, id, Name, email, and Oauth id, we will use outh authentication so we won't need password or anything else
The next databse is AuditLogs, this database is a save of all actions in the database, add, update delete, in the other InventoryDb, this will have just a table that is for
Analytics, the table is AuditLog, with Id, type of change, oldvalue and new movement, fill this fields accordingly with the operation, serializing the json with the changes
This is for the database

Th project is divided in diferent layers, Docuker compose for the executingh of the diffwerent docker images, Inventory.Api for the restful Apis, Service for all the
bussines logic, repository for all ops in the database, Utils, for any useful code that is transversal for all projects, and Inventory.Domain for all ApiModels
Contants, Models for the service, for the repository create the describe database using code first definition for entity framework, and seed some values for the prduct and inevntory
add contrainst for the quantities that cant be less than 0

we will use CQRS forninreactions with the database, for querying we will use entiryFramewok and for transaction we will use dappper, use transactions for inventory updates, to ensure ACID principles
Create a ProductApi, and a AuditLog api, the auditlog should return the data with paginitaion, should be a get, and for inventory, create different APIS with versioning
for return products, movements, update products, and update products categories

we will have a Inventory.UnitTest project, crate unit test for services and apis, checking all possible scenarios, foir the e2e test, create run the prroject
and execute the different APIS validing the different ops, the repository and service classes should have interfaces, and should use dependency injection
we are using open telemetry add metrics for inventory ops add, remove, by category and product, add try catch to avoid unecpetcted exceptions, return proper error messages
 using logging classes using source generation, and proper error messaging warning messaging
 Create the plan docs, divide the work by tasks, databse creating, seed, repository applying reading from entity framework, and transactions using dapper, service creating bussines logic and cqrs logic,
 Apis for products and the  auditilogs, add logging, mettrics, unit test and then e2e test, of course audit logs for the ops for all the table in the inventory database,
 add readme, and designdoc with mermaid diagrams,
 so first tash desgndoc, then readme and the others follow the order thaty I mentioned, we are using central package manager so if you need to add a package add tho the directory.props file
 and add the reference to the project

For authenticaltion use ouath2 usinmg keycloak, everything should run locally