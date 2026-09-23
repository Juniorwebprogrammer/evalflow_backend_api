using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Questions.DeleteQuestion;

public record DeleteQuestionRecord(int TemplateId, int QuestionId) : IRequest<IResult>;