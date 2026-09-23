using MediatR;

namespace evalflow_backend_api.Features.Profile.ChangePassword
{
    public record ChangePasswordRecord
    (
        string CurrentPassword,
        string NewPassword
    ) : IRequest<IResult>;
}