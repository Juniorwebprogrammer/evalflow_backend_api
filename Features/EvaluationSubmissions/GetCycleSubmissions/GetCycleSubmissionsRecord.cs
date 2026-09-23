using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetCycleSubmissions;

public record GetCycleSubmissionsRecord(int CycleId) : IRequest<IResult>;
