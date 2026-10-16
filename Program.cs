using evalflow_backend_api.Core.Extensions;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Features.AdminApi;
using evalflow_backend_api.Infrastructure.Logging;
using evalflow_backend_api.Infrastructure.Plans;

var builder = WebApplication.CreateBuilder(args);

// --- 1. REGISTRO DE SERVICIOS (DI) ---
builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddSignalR();

var app = builder.Build();

// Herramientas de administración: `dotnet run -- create-admin <email> <nombre> <apellidos>` en vez de la API.
if (args.Length > 0 && args[0] == CreateAdministratorCli.Command)
{
    return await CreateAdministratorCli.RunAsync(app.Services, args);
}

// --- 2. PIPELINE DE PETICIONES HTTP ---
app.UseMiddleware<RequestLoggingMiddleware>();
// Admin API (/admin/v1): X-Request-Id, Cache-Control: no-store y errores problem+json, antes de la autenticación.
app.UseAdminApi();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// En contenedor (Koyeb / docker compose) el TLS lo termina el proxy de delante y la app solo escucha HTTP.
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// Sin plan activo en Vexxen, la empresa queda en solo lectura (403 PLAN_INACTIVE en las escrituras).
app.UseMiddleware<PlanAccessMiddleware>();

// --- 3. MAPEADO DE RUTAS ---
app.MapApiEndpoints();
app.MapHubExtensions();

// --- 4. INICIALIZACIÓN Y CHEQUEOS ---
await app.CheckDatabaseConnectionAsync();

app.Run();
return 0;
