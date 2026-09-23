using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Templates.GetTemplateById;

public record GetTemplateByIdRecord(int Id) : IRequest<IResult>;