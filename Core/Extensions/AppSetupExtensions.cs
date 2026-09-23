using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Core.Extensions;

public static class AppSetupExtensions
{
    // 1. Método para registrar los servicios (DI)
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddOpenApi();

        var connectionString = config.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AppSetupExtensions).Assembly));

        return services;
    }

    // 2. Método para la comprobación de la base de datos
    public static async Task CheckDatabaseConnectionAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();

        // Sin cadena de conexión no tiene sentido arrancar: fallamos para que el deploy
        // (Koyeb, docker compose...) se marque como fallido en vez de quedar "healthy" sin DB.
        var dbContext = services.GetRequiredService<AppDbContext>();
        if (dbContext.Database.IsRelational() && string.IsNullOrWhiteSpace(dbContext.Database.GetConnectionString()))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection' " +
                "(variable de entorno ConnectionStrings__DefaultConnection).");
        }

        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            logger.LogInformation("🔄 Intentando conectar a Neon PostgreSQL...");
            
            if (await context.Database.CanConnectAsync())
            {
                logger.LogInformation("=================================================");
                logger.LogInformation("✅ ¡CONEXIÓN EXITOSA A NEON POSTGRESQL! 🎉");
                logger.LogInformation("=================================================");
            }
            else
            {
                logger.LogError("❌ El servidor de Neon respondió, pero no se pudo acceder a la base de datos.");
            }

            try
            {
                context.Database.Migrate();
                logger.LogInformation("=====================================================");
                logger.LogInformation("✅ ¡Migración completada con éxito! 🎉");
                logger.LogInformation("=====================================================");
            }
            catch (Exception e)
            {
                services.GetRequiredService<ILogger<Program>>();
                logger.LogError(e, "Error al migrar la base de datos");
            }
            
        }
        catch (Exception ex)
        {
            logger.LogError("=================================================");
            logger.LogError(ex, "💥 ERROR CRÍTICO AL CONECTAR A NEON POSTGRESQL");
            logger.LogError("=================================================");
        }
    }
}