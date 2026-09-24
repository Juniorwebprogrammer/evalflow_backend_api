using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationResults.GetCycleEvaluationResults;

public record GetCycleEvaluationResultsRecord(int CycleId) : IRequest<IResult>;
