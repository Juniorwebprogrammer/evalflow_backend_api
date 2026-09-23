using evalflow_backend_api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.GetMyFeatures;

public class GetMyFeaturesHandler(AppDbContext dbContext) : IRequestHandler<GetMyFeaturesRecord, IResult>
{
    public async Task<IResult> Handle(GetMyFeaturesRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            return Results.Unauthorized();
        }
        
        var roleData = await FetchRoleDataAsync(request.RoleName, cancellationToken);

        if (roleData is null)
        {
            return Results.NotFound(new { Message = "No se encontró el rol especificado." });
        }

        if (!roleData.Value.Features.Any()) 
        {
            return Results.NotFound(new { Message = "No se encontraron permisos para el rol seleccionado." });
        }

        return GenerateSuccessResponse(request.RoleName, roleData.Value.Description, roleData.Value.Features);
    }

    private async Task<(string Description, List<GetMyFeatureResponse> Features)?> FetchRoleDataAsync(string roleName, CancellationToken cancellationToken)
    {
        var result = await dbContext.Roles
            .Where(rol => rol.Name == roleName)
            .Select(rol => new 
            {
                rol.Description,
                Features = rol.Features.Select(f => new GetMyFeatureResponse(f.FeatureCode, f.Description)).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null) return null;

        return (result.Description, result.Features);
    }

    private static IResult GenerateSuccessResponse(string roleName, string description, List<GetMyFeatureResponse> features)
    {
        return Results.Ok(new
        {
            Role = roleName,
            Description = description,
            Features = features
        });
    }
}