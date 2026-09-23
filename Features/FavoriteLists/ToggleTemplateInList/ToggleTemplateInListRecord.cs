using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.FavoriteLists.ToggleTemplateInList;

public record ToggleTemplateInListRecord(int ListId, int TemplateId) : IRequest<IResult>;