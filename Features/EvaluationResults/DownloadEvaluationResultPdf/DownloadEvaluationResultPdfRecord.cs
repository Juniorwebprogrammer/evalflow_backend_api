using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;

public record DownloadEvaluationResultPdfRecord(int ResultId) : IRequest<IResult>;
