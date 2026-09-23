using MediatR;

namespace evalflow_backend_api.Features.Auth.ResendVerification;

public record ResendVerificationRecord(string Email) : IRequest<IResult>;