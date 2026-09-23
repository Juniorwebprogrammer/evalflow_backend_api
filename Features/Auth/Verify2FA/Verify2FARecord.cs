using MediatR;

namespace evalflow_backend_api.Features.Auth.Verify2FA;

public record Verify2FARecord(string Email, string Code) : IRequest<IResult>;