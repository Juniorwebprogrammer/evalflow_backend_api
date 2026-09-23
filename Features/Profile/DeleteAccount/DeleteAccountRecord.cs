using MediatR;

namespace evalflow_backend_api.Features.Profile.DeleteAccount;

public record DeleteAccountRecord(
    string Password
) : IRequest<IResult>;