using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetMyCompletedSubmissions;

public record GetMyCompletedSubmissionsRecord() : IRequest<IResult>;