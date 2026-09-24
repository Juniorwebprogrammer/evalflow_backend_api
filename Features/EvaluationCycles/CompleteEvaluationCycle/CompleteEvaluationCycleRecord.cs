using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationCycles.CompleteEvaluationCycle;

public record CompleteEvaluationCycleRecord(int CycleId) : IRequest<IResult>;

public record CompleteEvaluationCycleResponse(string Message, int ResultsGenerated, int AutoCompletedSubmissions, DateTime CompletedAt);
