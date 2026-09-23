using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.FavoriteLists.DeleteFavoriteList;

public record DeleteFavoriteListRecord(int Id) : IRequest<IResult>;