using MediatR;

namespace evalflow_backend_api.Features.Templates.DeleteTemplates;

public record DeleteTemplateRecord(int Id) : IRequest<IResult>;