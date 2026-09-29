using MediatR;

namespace evalflow_backend_api.Features.AiAnalysis.GetCycleAiAnalyses;

/// <summary>The latest AI analysis of each employee (and template) of the cycle.</summary>
public record GetCycleAiAnalysesRecord(int CycleId, int? EvaluatedUserId) : IRequest<IResult>;
