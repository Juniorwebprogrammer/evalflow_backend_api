using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Templates.GetAllTemplates;

public record GetAllTemplatesRecord() : IRequest<IResult>;