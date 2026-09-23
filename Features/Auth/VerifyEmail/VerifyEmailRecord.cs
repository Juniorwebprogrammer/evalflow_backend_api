using MediatR;

namespace evalflow_backend_api.Features.Auth.VerifyEmail;

public record VerifyEmailRecord(string Token) : IRequest<IResult>;