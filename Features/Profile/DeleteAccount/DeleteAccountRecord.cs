using MediatR;

namespace evalflow_backend_api.Features.Profile.DeleteAccount;

/// <summary>Confirmed by typing the account's email (there's no EvalFlow password any more). The Vexxen account stays.</summary>
public record DeleteAccountRecord(
    string ConfirmEmail
) : IRequest<IResult>;
