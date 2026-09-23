using evalflow_backend_api.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;

public record UpdateEvaluationCycleBody(
    string Nombre, string? Descripcion, bool Activo, DateTime FechaInicio, DateTime FechaFin,
    EvaluationType TipoEvaluacion = EvaluationType.Evaluacion360);
public record UpdateEvaluationCycleRecord(
    int Id, string Nombre, string? Descripcion, bool Activo, DateTime FechaInicio, DateTime FechaFin,
    EvaluationType TipoEvaluacion = EvaluationType.Evaluacion360) : IRequest<IResult>;