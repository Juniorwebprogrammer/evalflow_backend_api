using MediatR;

namespace evalflow_backend_api.Features.Templates.UpdateTemplates;

public record UpdateTemplateBody(string Titulo, string? Descripcion, DateTime FechaInicio, DateTime FechaFin, List<int> AssignedUserIds);

public record UpdateTemplateRecord(int Id, string Titulo, string? Descripcion, DateTime FechaInicio, DateTime FechaFin, List<int> AssignedUserIds) : IRequest<IResult>;