using MediatR;

namespace evalflow_backend_api.Features.AiAnalysis.RequestAiAnalysis;

/// <summary>
/// Queues an AI analysis for every employee of the cycle with completed answers, or only for
/// <paramref name="EvaluatedUserId"/> / <paramref name="TemplateId"/>. Unchanged evaluations reuse
/// their last analysis unless <paramref name="Force"/> is set.
/// </summary>
public record RequestAiAnalysisRecord(int CycleId, int? EvaluatedUserId, int? TemplateId, bool Force) : IRequest<IResult>;
