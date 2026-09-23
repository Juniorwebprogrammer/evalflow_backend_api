using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetMyPendingSubmissions;

public record GetMyPendingSubmissionsRecord() : IRequest<IResult>;