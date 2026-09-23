using MediatR;

namespace evalflow_backend_api.Features.JobPosittions.UpdateJobPosition;

public record UpdateJobPositionBody(string Nombre, string? Descripcion);
public record UpdateJobPositionRecord(int Id, string Nombre, string? Descripcion) : IRequest<IResult>;