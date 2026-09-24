using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Clarifications.GetMyClarifications;

public record GetMyClarificationsRecord() : IRequest<IResult>;
