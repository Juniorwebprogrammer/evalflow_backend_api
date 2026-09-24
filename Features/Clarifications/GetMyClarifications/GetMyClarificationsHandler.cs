using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Clarifications.GetMyClarifications;

public class GetMyClarificationsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<GetMyClarificationsHandler> logger
) : IRequestHandler<GetMyClarificationsRecord, IResult>
{
    public async Task<IResult> Handle(GetMyClarificationsRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var clarifications = await FetchClarificationsAsync(userId, cancellationToken);

        return Results.Ok(clarifications.Select(c => MapClarification(c, userId)).ToList());
    }

    private async Task<List<ClarificationRequest>> FetchClarificationsAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.ClarificationRequests
            .AsNoTracking()
            .Where(c => c.EvaluatedUserId == userId || c.ManagerUserId == userId)
            .Include(c => c.Cycle)
            .Include(c => c.Template)
            .Include(c => c.Question)
            .Include(c => c.EvaluatedUser)
            .Include(c => c.RequestedByUser)
            .OrderByDescending(c => c.FechaCreacion)
            .ToListAsync(cancellationToken);
    }

    private MyClarificationDto MapClarification(ClarificationRequest clarification, int userId)
    {
        var isEvaluated = clarification.EvaluatedUserId == userId;

        return new MyClarificationDto(
            clarification.Id,
            clarification.Cycle!.Nombre,
            clarification.Template!.Titulo,
            clarification.Question?.Texto,
            $"{clarification.EvaluatedUser!.Nombre} {clarification.EvaluatedUser.Apellidos}",
            isEvaluated ? ClarificationParticipant.Evaluado : ClarificationParticipant.Evaluador,
            $"{clarification.RequestedByUser!.Nombre} {clarification.RequestedByUser.Apellidos}",
            clarification.Mensaje,
            DecryptResponse(isEvaluated ? clarification.EvaluatedResponseEncrypted : clarification.ManagerResponseEncrypted, clarification.Id),
            isEvaluated ? clarification.EvaluatedRespondedAt : clarification.ManagerRespondedAt,
            clarification.FechaCreacion
        );
    }

    private string? DecryptResponse(string? encrypted, int clarificationId)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;

        try
        {
            return encryptionService.Decrypt(encrypted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetMyClarifications: no se pudo descifrar la respuesta de la solicitud {ClarificationId}.", clarificationId);
            return null;
        }
    }
}
