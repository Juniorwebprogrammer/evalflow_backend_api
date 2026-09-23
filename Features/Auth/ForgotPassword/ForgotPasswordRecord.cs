using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Auth.ForgotPassword;

public record ForgotPasswordBody(string Email);
public record ForgotPasswordRecord(string Email) : IRequest<IResult>;