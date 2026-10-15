using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.GetInvitation;

/// <summary>
/// The invitation behind the link in the email, so the accept page can say "Join Acme: sign in with Vexxen as
/// ana@acme.com". Accepting is just signing in with Vexxen using that email (POST /Auth/vexxen links the account).
/// </summary>
public record GetInvitationRecord(string Token) : IRequest<IResult>;

public record InvitationDto(string CompanyNombre, string CompanyColors, string Nombre, string Email, bool Expired);

public class GetInvitationHandler(AppDbContext dbContext) : IRequestHandler<GetInvitationRecord, IResult>
{
    public async Task<IResult> Handle(GetInvitationRecord request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) return NotFound();

        var invitation = await dbContext.Users
            .Where(u => u.TokenInvitacion == request.Token)
            .Select(u => new InvitationDto(
                u.Empresa!.Nombre,
                u.Empresa!.Colors,
                u.Nombre,
                u.Email,
                u.TokenInvitacionExpiracion < DateTime.UtcNow))
            .FirstOrDefaultAsync(cancellationToken);

        return invitation is null ? NotFound() : Results.Ok(invitation);
    }

    // Also when it was already accepted: the token is cleared then.
    private static IResult NotFound() =>
        Results.NotFound(new { Code = "INVITATION_NOT_FOUND", Message = "Esta invitación no existe o ya se ha aceptado." });
}

public static class GetInvitationEndpoint
{
    public static void MapGetInvitation(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Team/invitation/{token}", async (string token, ISender sender) => await sender.Send(new GetInvitationRecord(token)))
            .WithTags("Team")
            .WithSummary("Datos de una invitación (para la pantalla de aceptarla)")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .AllowAnonymous();
    }
}
