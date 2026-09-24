using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Clarifications.RespondClarification;

public class RespondClarificationHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService
) : IRequestHandler<RespondClarificationRecord, IResult>
{
    private const int MaxRespuestaLength = 2000;

    public async Task<IResult> Handle(RespondClarificationRecord request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(currentUser.GetUserId(), out var userId)) return Results.Unauthorized();

        var validationError = ValidateRespuesta(request.Respuesta);
        if (validationError is not null) return validationError;

        var clarification = await GetClarificationAsync(request.ClarificationId, userId, cancellationToken);
        if (clarification is null) return Results.NotFound(new { Message = "Solicitud de información no encontrada." });

        var participant = clarification.EvaluatedUserId == userId
            ? ClarificationParticipant.Evaluado
            : ClarificationParticipant.Evaluador;

        if (HasResponded(clarification, participant))
            return Results.Conflict(new { Message = "Ya has respondido a esta solicitud." });

        var respuesta = request.Respuesta.Trim();
        ApplyResponse(clarification, participant, respuesta);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(MapClarification(clarification, participant, respuesta));
    }

    private static IResult? ValidateRespuesta(string? respuesta)
    {
        if (string.IsNullOrWhiteSpace(respuesta))
            return Results.BadRequest(new { Message = "La respuesta no puede estar vacía." });

        if (respuesta.Trim().Length > MaxRespuestaLength)
            return Results.BadRequest(new { Message = $"La respuesta no puede superar los {MaxRespuestaLength} caracteres." });

        return null;
    }

    private async Task<ClarificationRequest?> GetClarificationAsync(int clarificationId, int userId, CancellationToken cancellationToken)
    {
        return await dbContext.ClarificationRequests
            .Include(c => c.Cycle)
            .Include(c => c.Template)
            .Include(c => c.Question)
            .Include(c => c.EvaluatedUser)
            .Include(c => c.RequestedByUser)
            .FirstOrDefaultAsync(c => c.Id == clarificationId
                                      && (c.EvaluatedUserId == userId || c.ManagerUserId == userId), cancellationToken);
    }

    private static bool HasResponded(ClarificationRequest clarification, ClarificationParticipant participant)
    {
        return participant == ClarificationParticipant.Evaluado
            ? clarification.EvaluatedRespondedAt.HasValue
            : clarification.ManagerRespondedAt.HasValue;
    }

    private void ApplyResponse(ClarificationRequest clarification, ClarificationParticipant participant, string respuesta)
    {
        var encrypted = encryptionService.Encrypt(respuesta);
        var now = DateTime.UtcNow;

        if (participant == ClarificationParticipant.Evaluado)
        {
            clarification.EvaluatedResponseEncrypted = encrypted;
            clarification.EvaluatedRespondedAt = now;
        }
        else
        {
            clarification.ManagerResponseEncrypted = encrypted;
            clarification.ManagerRespondedAt = now;
        }

        clarification.Estado = clarification.EvaluatedRespondedAt.HasValue && clarification.ManagerRespondedAt.HasValue
            ? ClarificationStatus.Respondida
            : ClarificationStatus.Parcial;
    }

    private static MyClarificationDto MapClarification(ClarificationRequest clarification, ClarificationParticipant participant, string respuesta)
    {
        return new MyClarificationDto(
            clarification.Id,
            clarification.Cycle!.Nombre,
            clarification.Template!.Titulo,
            clarification.Question?.Texto,
            $"{clarification.EvaluatedUser!.Nombre} {clarification.EvaluatedUser.Apellidos}",
            participant,
            $"{clarification.RequestedByUser!.Nombre} {clarification.RequestedByUser.Apellidos}",
            clarification.Mensaje,
            respuesta,
            participant == ClarificationParticipant.Evaluado ? clarification.EvaluatedRespondedAt : clarification.ManagerRespondedAt,
            clarification.FechaCreacion
        );
    }
}
