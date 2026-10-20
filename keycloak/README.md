# Local Keycloak

Compose imports the `inventory` realm and `inventory-api` public OIDC client. The realm issuer is exactly `http://keycloak:8080/realms/inventory`, and the API validates that issuer and the `inventory-api` audience. The API is configured with the `keycloak` hostname, not `localhost`.

The local Keycloak admin console uses the fixed development credentials `local-admin` / `InventoryLocal_2026!`. They are for a developer workstation only.

The imported `inventory` realm includes the local API test user `inventory-user` / `InventoryUser_2026!`, with a verified email and permanent password so the sample direct token request works immediately. These credentials are local-demo-only and must not be used outside an isolated development environment. The API test user is distinct from the Keycloak admin account.

## Host access

On Windows, add this line to `C:\Windows\System32\drivers\etc\hosts` using an elevated editor:

```text
127.0.0.1 keycloak
```

This makes the host-side Keycloak URL `http://keycloak:8080` while preserving the same issuer hostname used inside Compose. Do not change the API authority to `http://localhost:8080`; tokens still identify their issuer as `http://keycloak:8080`.

## Create a test user

1. Use the imported `inventory-user` account for local API requests, or open `http://keycloak:8080`, select the `inventory` realm, and open **Users** to create another user.
2. For another user, set a username, first name, last name, email, verified email, enabled status, a permanent password, and no pending required actions.

These account settings matter for local direct token requests: a new/incomplete user can cause Keycloak to return `invalid_grant` with “Account is not fully set up.”

## Get a local access token

The imported client allows password grant for local development only. From PowerShell:

```powershell
$userName = 'inventory-user'
$password = 'InventoryUser_2026!'
$tokenResponse = Invoke-RestMethod -Method Post `
  -Uri 'http://keycloak:8080/realms/inventory/protocol/openid-connect/token' `
  -ContentType 'application/x-www-form-urlencoded' `
  -Body @{ grant_type = 'password'; client_id = 'inventory-api'; username = $userName; password = $password }
$env:INVENTORY_ACCESS_TOKEN = $tokenResponse.access_token
```

Keep the access token in the same shell used to run `dotnet test Inventory.E2ETest\Inventory.E2ETest.csproj`. Do not paste it into committed files, logs, or screenshots. The Keycloak direct grant and development account are for local testing, not production authentication UX.
