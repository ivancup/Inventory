param(
    [switch]$Detached,
    [switch]$Down,
    [switch]$Volumes
)

$ErrorActionPreference = "Stop"
$settingsPath = Join-Path $PSScriptRoot "..\Inventory.Api\appsettings.Local.json"

if (-not (Test-Path $settingsPath)) {
    throw "Create Inventory.Api\appsettings.Local.json from appsettings.Local.example.json first."
}

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$password = [string]$settings.LocalServices.MssqlSaPassword
$keycloakPassword = [string]$settings.LocalServices.KeycloakAdminPassword
if ([string]::IsNullOrWhiteSpace($password) -or [string]::IsNullOrWhiteSpace($keycloakPassword)) {
    throw "Set LocalServices.MssqlSaPassword and KeycloakAdminPassword in Inventory.Api\appsettings.Local.json."
}

$env:MSSQL_SA_PASSWORD = $password
$env:KEYCLOAK_ADMIN_USERNAME = [string]$settings.LocalServices.KeycloakAdminUsername
$env:KEYCLOAK_ADMIN_PASSWORD = $keycloakPassword
$env:ConnectionStrings__Inventory = ([string]$settings.ConnectionStrings.Inventory -replace '(?i)Server=localhost', 'Server=sqlserver') -replace '(?i)Password=[^;]*', "Password=$password"
$env:ConnectionStrings__AuditLogs = ([string]$settings.ConnectionStrings.AuditLogs -replace '(?i)Server=localhost', 'Server=sqlserver') -replace '(?i)Password=[^;]*', "Password=$password"
$env:INVENTORY_DB_CONNECTION = ([string]$settings.ConnectionStrings.Inventory -replace '(?i)Server=localhost', 'Server=127.0.0.1') -replace '(?i)Password=[^;]*', "Password=$password"
$env:Authentication__Authority = [string]$settings.Authentication.Authority
$env:Authentication__Audience = [string]$settings.Authentication.Audience

Push-Location (Join-Path $PSScriptRoot "..")
try {
    if ($Down) {
        if ($Volumes) {
            docker compose down --volumes
        }
        else {
            docker compose down
        }
    }
    elseif ($Detached) {
        docker compose up --build --detach
    }
    else {
        docker compose up --build
    }

    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
