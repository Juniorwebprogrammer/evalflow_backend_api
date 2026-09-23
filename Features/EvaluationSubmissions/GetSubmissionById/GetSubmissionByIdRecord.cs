using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationSubmissions.GetSubmissionById;

public record GetSubmissionByIdRecord(int SubmissionId) : IRequest<IResult>;