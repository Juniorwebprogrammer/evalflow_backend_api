using MediatR;

namespace evalflow_backend_api.Features.JobPosittions.DeleteJobPosition;

public record DeleteJobPositionRecord(int Id) : IRequest<IResult>;