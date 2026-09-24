using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationResults.GetMyEvaluationResults;

public record GetMyEvaluationResultsRecord() : IRequest<IResult>;
