using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Team.GetSubordinates;

public record GetSubordinatesRecord(int UserId) : IRequest<IResult>;
public record SubordinateDto(int Id, string Nombre, string Apellidos, string Email, string Rol, string Cargo, bool Activo);