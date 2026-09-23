using evalflow_backend_api.Core.Extensions; 

var builder = WebApplication.CreateBuilder(args);

// --- 1. REGISTRO DE SERVICIOS (DI) ---
builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddSignalR();

var app = builder.Build();

// --- 2. PIPELINE DE PETICIONES HTTP ---
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

// --- 3. MAPEADO DE RUTAS ---
app.MapApiEndpoints();
app.MapHubExtensions();

// --- 4. INICIALIZACIÓN Y CHEQUEOS ---
await app.CheckDatabaseConnectionAsync();

app.Run();