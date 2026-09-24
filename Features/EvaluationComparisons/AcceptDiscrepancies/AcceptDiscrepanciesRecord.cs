using evalflow_backend_api.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationComparisons.AcceptDiscrepancies;

public record AcceptDiscrepanciesBody(int EvaluatedUserId, int TemplateId, List<int> QuestionIds, AcceptedAnswerSource Source);

public record AcceptDiscrepanciesRecord(
    int CycleId, int EvaluatedUserId, int TemplateId, List<int> QuestionIds, AcceptedAnswerSource Source) : IRequest<IResult>;

public record AcceptedDiscrepancyDto(int QuestionId, AcceptedAnswerSource Source, DateTime AcceptedAt);
