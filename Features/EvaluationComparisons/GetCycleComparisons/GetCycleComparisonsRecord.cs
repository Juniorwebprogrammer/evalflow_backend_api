using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;

public record GetCycleComparisonsRecord(int CycleId, int? EvaluatedUserId) : IRequest<IResult>;
