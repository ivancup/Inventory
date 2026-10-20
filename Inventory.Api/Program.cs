using Inventory.Api.DependencyInjection;
using Inventory.Api.Middleware;
using Inventory.Repository.Data;
using Inventory.Service;
using Inventory.Service.Interfaces;
using Inventory.Service.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

const string serviceName = "inventory";
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInventoryRepository(builder.Configuration);
builder.Services.AddSingleton<InventoryMetrics>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters.NameClaimType = "name";
    });
builder.Services.AddAuthorization(options =>
{
    var authenticatedInventoryUser = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("sub")
        .Build();
    options.AddPolicy("InventoryWrite", authenticatedInventoryUser);
    options.FallbackPolicy = authenticatedInventoryUser;
});

builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
        .AddConsoleExporter();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("Inventory.Management")
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
