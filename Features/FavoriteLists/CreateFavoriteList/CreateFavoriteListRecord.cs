using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.FavoriteLists.CreateFavoriteList;

public record CreateFavoriteListBody(string Nombre, string? Descripcion);
public record CreateFavoriteListRecord(string Nombre, string? Descripcion) : IRequest<IResult>;