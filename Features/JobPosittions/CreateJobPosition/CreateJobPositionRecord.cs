using MediatR;

namespace evalflow_backend_api.Features.JobPosittions.CreateJobPosition;

public record CreateJobPositionRecord(string Nombre, string? Descripcion) : IRequest<IResult>;