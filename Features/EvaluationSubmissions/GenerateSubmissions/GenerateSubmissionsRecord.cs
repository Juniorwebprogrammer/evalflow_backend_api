using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GenerateSubmissions;

public record GenerateSubmissionsRecord(int CycleId) : IRequest<IResult>;