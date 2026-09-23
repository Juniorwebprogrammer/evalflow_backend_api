using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationCycles.DeleteEvaluationCycle;

public record DeleteEvaluationCycleRecord(int Id) : IRequest<IResult>;