using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Questions.GetQuestionsByTemplate;

public record GetQuestionsByTemplateRecord(int TemplateId) : IRequest<IResult>;