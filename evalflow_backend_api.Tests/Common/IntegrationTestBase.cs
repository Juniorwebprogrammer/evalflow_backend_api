using System.Net.Http.Headers;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Jwt;
using Microsoft.Extensions.DependencyInjection;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Base class for endpoint (HTTP-level) integration tests. Each test class gets its own
/// CustomWebApplicationFactory (and therefore its own in-memory database), so tests never leak
/// state into one another.
///
/// The API requires the "X-Api-Key" header on every endpoint (see ApiKeyEndpointFilter), so
/// Client already carries a valid one. Endpoints that also require a signed-in user should use
/// CreateAuthenticatedClient.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    public const string ValidApiKey = "EvalFlow-Public-Key-987654321";

    protected CustomWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase()
    {
        Factory = new CustomWebApplicationFactory();
        Client = CreateClient();
    }

    /// <summary>An HttpClient with a valid X-Api-Key but no bearer token (anonymous caller).</summary>
    protected HttpClient CreateClient(bool includeApiKey = true)
    {
        var client = Factory.CreateClient();
        if (includeApiKey)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        }

        return client;
    }

    /// <summary>An HttpClient carrying a valid X-Api-Key and a JWT for the given user/company.</summary>
    protected HttpClient CreateAuthenticatedClient(User user, Company company)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwt(user, company));
        return client;
    }

    /// <summary>Generates a real JWT (same provider/config the app uses) for a given user/company.</summary>
    protected string GenerateJwt(User user, Company company)
    {
        using var scope = Factory.Services.CreateScope();
        var jwtProvider = scope.ServiceProvider.GetRequiredService<IJwtProvider>();
        return jwtProvider.GenerateJwt(user, company);
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
