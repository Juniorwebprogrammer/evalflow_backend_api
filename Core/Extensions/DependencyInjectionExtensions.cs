using System.Text;
using evalflow_backend_api.Infrastructure.BackgroundJobs;
using evalflow_backend_api.Infrastructure.Jwt;
using evalflow_backend_api.Infrastructure.Notifications;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace evalflow_backend_api.Core.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Servicios de Dominio/Infraestructura
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPasswordHasser, PasswordHasher>();
        services.AddScoped<IEncryptionService, AesEncryptionService>();
        
        // Agregar servicios core (tu extensión existente)
        services.AddCoreServices(configuration);

        // Configuración de Autenticación y JWT
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!))
                };

                // Los clientes de SignalR (navegador) no pueden enviar el header
                // Authorization en el handshake de WebSocket, así que el JS client
                // manda el token por query string (?access_token=...) y aquí lo
                // recuperamos para que [Authorize] en los hubs funcione.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // The frontend's own API routes always call us server-to-server, so
        // they never needed CORS — but the SignalR connection is opened
        // directly from the browser (Route Handlers can't keep a WebSocket
        // alive), which makes it a real cross-origin request. Scoped to the
        // hub only (see `RequireCors("SignalR")` on its MapHub call); no
        // credentials because the hub authenticates via a bearer token in
        // the query string, not cookies.
        services.AddCors(options =>
        {
            options.AddPolicy("SignalR", policy =>
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        services.AddHttpClient<IEmailService, BrevoApiEmailService>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpContextAccessor();
        services.AddHostedService<EvaluationReminderJob>();
        
        return services;
    }
}