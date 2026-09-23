using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.FavoriteLists.GetMyFavoriteLists;

public record GetMyFavoriteListsRecord() : IRequest<IResult>;