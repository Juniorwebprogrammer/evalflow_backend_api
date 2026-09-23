using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationCycles.GetAllEvaluationCycles;

public record GetAllEvaluationCyclesRecord() : IRequest<IResult>;