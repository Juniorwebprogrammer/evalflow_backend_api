using evalflow_backend_api.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;

public record CreateEvaluationCycleBody(
    string Nombre, string? Descripcion, DateTime FechaInicio, DateTime FechaFin,
    EvaluationType TipoEvaluacion = EvaluationType.Evaluacion360);

public record CreateEvaluationCycleRecord(
    string Nombre, string? Descripcion, DateTime FechaInicio, DateTime FechaFin,
    EvaluationType TipoEvaluacion = EvaluationType.Evaluacion360) : IRequest<IResult>;