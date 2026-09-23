using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using MediatR;

namespace evalflow_backend_api.Features.EvaluationSubmissions.SaveSubmissionAnswers;

public record SaveSubmissionAnswersBody(List<AnswerInputDto> Answers);
public record SaveSubmissionAnswersRecord(int SubmissionId, List<AnswerInputDto> Answers) : IRequest<IResult>;