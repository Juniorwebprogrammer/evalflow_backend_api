using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Templates.CreateTemplate;

public record CreateTemplateBody(string Titulo, string? Descripcion, DateTime FechaInicio, DateTime FechaFin, List<int> AssignedUserIds);

public record CreateTemplateRecord(string Titulo, string? Descripcion, DateTime FechaInicio, DateTime FechaFin, List<int> AssignedUserIds) : IRequest<IResult>;