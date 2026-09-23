using MediatR;

namespace evalflow_backend_api.Features.Settings.Toggle2FA;

public record Toggle2FARecord(int UserId, bool Enable) : IRequest<IResult>;