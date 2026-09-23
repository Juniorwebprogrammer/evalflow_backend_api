namespace evalflow_backend_api.Infrastructure.Security;

public class ApiKeyEndpointFilter(IConfiguration configuration) : IEndpointFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var extractedApiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(extractedApiKey))
        {
            return Results.Unauthorized();
        }
        
        var expectedApiKey = configuration["ApiKey"];

        if (!extractedApiKey.Equals(expectedApiKey))
        {
            return Results.Unauthorized();
        }
        
        return await next(context);
    }
}