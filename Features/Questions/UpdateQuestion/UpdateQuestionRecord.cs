using evalflow_backend_api.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Questions.UpdateQuestion;

public record UpdateQuestionBody(string Texto, string Topic, QuestionType Tipo, List<string>? Opciones, int Orden);

public record UpdateQuestionRecord(int TemplateId, int QuestionId, string Texto, string Topic, QuestionType Tipo, List<string>? Opciones, int Orden) : IRequest<IResult>;