using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.AdminApi.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace evalflow_backend_api.Features.AdminApi.Access;

/// <summary>
/// Where the Vexxen Administrator tokens come from (variables VexxenAdmin__Issuer and VexxenAdmin__Audience).
/// Issuer: the Vexxen base URL (its iss; the signing keys are discovered from /.well-known/openid-configuration).
/// Audience: EvalFlow's appId in Vexxen. Environment (optional): what the manifest reports, by default derived from
/// ASPNETCORE_ENVIRONMENT (Production → production, Development → local).
/// </summary>
public class VexxenAdminSettings
{
    public const string SectionName = "VexxenAdmin";

    public string? Issuer { get; set; }
    public string Audience { get; set; } = "evalflow";
    public string? Environment { get; set; }
}

/// <summary>
/// The Admin API authenticates ONLY with the access tokens of Vexxen Administrator (contract §5.2): ES256, typ at+jwt,
/// iss and aud as configured, exp/nbf with 60 s of tolerance. It is a scheme apart from EvalFlow's own JWT, which stays
/// the default: EvalFlow's JWT never reaches /admin/v1 and an admin token never reaches the rest of the API.
/// </summary>
public static class VexxenAdminAuthentication
{
    public const string Scheme = "VexxenAdmin";
    public const string Policy = "VexxenAdmin";

    public static IServiceCollection AddVexxenAdminAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VexxenAdminSettings>().Bind(configuration.GetSection(VexxenAdminSettings.SectionName));

        // Registered without changing the default scheme (EvalFlow's JwtBearerDefaults.AuthenticationScheme).
        services.AddAuthentication().AddJwtBearer(Scheme, _ => { });
        services.AddOptions<JwtBearerOptions>(Scheme)
            .Configure<IOptions<VexxenAdminSettings>, IHostEnvironment>((options, settings, environment) =>
                Configure(options, settings.Value, environment));

        services.AddAuthorization(options =>
            options.AddPolicy(Policy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()));

        return services;
    }

    internal static void Configure(JwtBearerOptions options, VexxenAdminSettings settings, IHostEnvironment environment)
    {
        var issuer = settings.Issuer?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(issuer)) issuer = null;

        // Without an issuer there are no keys: every token is rejected (401), the rest of the API is unaffected.
        options.Authority = issuer;
        options.Audience = settings.Audience;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.SaveToken = false;

        var parameters = options.TokenValidationParameters;
        parameters.ValidateIssuer = true;
        parameters.ValidIssuer = issuer;
        parameters.ValidateAudience = true;
        parameters.ValidAudience = settings.Audience;
        parameters.ValidateLifetime = true;
        parameters.RequireExpirationTime = true;
        parameters.ValidateIssuerSigningKey = true;
        parameters.ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256];
        parameters.ValidTypes = ["at+jwt"];
        parameters.ClockSkew = TimeSpan.FromSeconds(60);
        parameters.NameClaimType = "sub";

        options.Events = new JwtBearerEvents
        {
            // 401 as problem+json with WWW-Authenticate: Bearer error="invalid_token" (contract §6.7).
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var hasToken = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
                await AdminProblem.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, AdminProblemCodes.Unauthorized,
                    hasToken ? "The access token is not valid or has expired" : "Missing access token");
            },
        };
    }

    /// <summary>The environment the manifest reports: VexxenAdmin:Environment, or production / local from the host.</summary>
    public static string EnvironmentName(VexxenAdminSettings settings, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(settings.Environment)) return settings.Environment.Trim();
        if (environment.IsProduction()) return "production";
        if (environment.IsDevelopment()) return "local";
        return environment.EnvironmentName.ToLowerInvariant();
    }
}
