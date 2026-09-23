using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Profile.GetProfileInformation;

public class GetProfileInformationHandler(AppDbContext dbContext, ICurrentUserService currentUserService) : IRequestHandler<GetProfileInformationRecord, IResult>
{
    public async Task<IResult> Handle(GetProfileInformationRecord request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
        
        var profile = await FetchUserProfileAsync(userId, cancellationToken);
        if (profile is null) return Results.NotFound();
        
        return Results.Ok(profile);
    }

    private async Task<GetProfileInformationDTO?> FetchUserProfileAsync(string userIdString,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(userIdString, out int userId)) return null;
        
        return await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => new GetProfileInformationDTO(
                user.Nombre,
                user.Apellidos,
                user.Email,
                user.Rol,
                user.FechaCreacion,
                user.Empresa!.Nombre,
                user.Empresa!.IdentificationId,
                user.TwoFactorEnabled
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}