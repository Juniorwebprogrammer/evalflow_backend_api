using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Auth.ResetPassword;

public record ResetPasswordBody(string Token, string NewPassword);
public record ResetPasswordRecord(string Token, string NewPassword) : IRequest<IResult>;