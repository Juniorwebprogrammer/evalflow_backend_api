using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationCycles.ToggleTemplateInCycle;

public record ToggleTemplateInCycleRecord(int CycleId, int TemplateId) : IRequest<IResult>;