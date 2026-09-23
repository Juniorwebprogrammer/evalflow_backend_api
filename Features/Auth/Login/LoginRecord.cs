using MediatR;

namespace evalflow_backend_api.Features.Auth.Login;

public record LoginRecord(
    string Email,
    string Password,
    string IdentificationId
) : IRequest<IResult>;

public record LoginResponse(
    string Message,
    string Username,
    string Jwt,
    string RefreshToken,
    bool TwoFactorEnabled
);