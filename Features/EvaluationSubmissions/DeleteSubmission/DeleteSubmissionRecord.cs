using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationSubmissions.DeleteSubmission;

public record DeleteSubmissionRecord(int SubmissionId) : IRequest<IResult>;