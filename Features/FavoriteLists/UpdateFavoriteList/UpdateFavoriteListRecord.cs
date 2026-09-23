using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.FavoriteLists.UpdateFavoriteList;

public record UpdateFavoriteListBody(string Nombre, string? Descripcion);
public record UpdateFavoriteListRecord(int Id, string Nombre, string? Descripcion) : IRequest<IResult>;