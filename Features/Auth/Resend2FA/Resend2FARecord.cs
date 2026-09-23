using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Auth.Resend2FA;

public record Resend2FARecord(string Email) : IRequest<IResult>;