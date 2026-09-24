using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Clarifications.ClarificationsDto;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Clarifications.GetCycleClarifications;

public class GetCycleClarificationsHandler(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<GetCycleClarificationsHandler> logger
) : IRequestHandler<GetCycleClarificationsRecord, IResult>
{
    public async Task<IResult> Handle(GetCycleClarificationsRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var cycleExists = await dbContext.EvaluationCycles
            .AnyAsync(c => c.Id == request.CycleId && c.Empresa!.IdentificationId == tenantId, cancellationToken);
        if (!cycleExists) return Results.NotFound(new { Message = "Ciclo no encontrado." });

        var clarifications = await FetchClarificationsAsync(request, cancellationToken);

        return Results.Ok(clarifications.Select(MapClarification).ToList());
    }

    private async Task<List<ClarificationRequest>> FetchClarificationsAsync(GetCycleClarificationsRecord request, CancellationToken cancellationToken)
    {
        var query = dbContext.ClarificationRequests
            .AsNoTracking()
            .Where(c => c.EvaluationCycleId == request.CycleId);

        if (request.EvaluatedUserId.HasValue)
            query = query.Where(c => c.EvaluatedUserId == request.EvaluatedUserId.Value);

        return await query
            .Include(c => c.Template)
            .Include(c => c.Question)
            .Include(c => c.EvaluatedUser)
            .Include(c => c.ManagerUser)
            .Include(c => c.RequestedByUser)
            .OrderByDescending(c => c.FechaCreacion)
            .ToListAsync(cancellationToken);
    }

    private ClarificationDto MapClarification(ClarificationRequest clarification)
    {
        return new ClarificationDto(
            clarification.Id,
            clarification.EvaluationCycleId,
            clarification.TemplateId,
            clarification.Template!.Titulo,
            clarification.QuestionId,
            clarification.Question?.Texto,
            clarification.EvaluatedUserId,
            $"{clarification.EvaluatedUser!.Nombre} {clarification.EvaluatedUser.Apellidos}",
            clarification.ManagerUserId,
            $"{clarification.ManagerUser!.Nombre} {clarification.ManagerUser.Apellidos}",
            $"{clarification.RequestedByUser!.Nombre} {clarification.RequestedByUser.Apellidos}",
            clarification.Mensaje,
            DecryptResponse(clarification.EvaluatedResponseEncrypted, clarification.Id),
            clarification.EvaluatedRespondedAt,
            DecryptResponse(clarification.ManagerResponseEncrypted, clarification.Id),
            clarification.ManagerRespondedAt,
            clarification.Estado,
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
            logger.LogWarning(ex, "GetCycleClarifications: no se pudo descifrar una respuesta de la solicitud {ClarificationId}.", clarificationId);
            return null;
        }
    }
}
