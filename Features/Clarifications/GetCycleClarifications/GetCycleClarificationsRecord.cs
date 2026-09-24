using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Clarifications.GetCycleClarifications;

public record GetCycleClarificationsRecord(int CycleId, int? EvaluatedUserId) : IRequest<IResult>;
