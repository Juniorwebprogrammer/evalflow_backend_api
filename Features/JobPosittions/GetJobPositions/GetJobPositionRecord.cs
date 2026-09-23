using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.JobPositions.GetJobPositions;

public record GetJobPositionsRecord() : IRequest<IResult>;