using evalflow_backend_api.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Questions.CreateQuestion;

public record CreateQuestionBody(string Texto, QuestionType Tipo, string Topic, List<string>? Opciones, int Orden);

public record CreateQuestionRecord(int TemplateId, string Texto, string Topic, QuestionType Tipo, List<string>? Opciones, int Orden) : IRequest<IResult>;