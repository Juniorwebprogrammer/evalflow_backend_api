using MediatR;

namespace evalflow_backend_api.Features.Onboarding.RegisterOwner;

public record RegisterOwnerRecord(
    string UserNombre,
    string Apellidos,
    string Email,
    string Password,
    string CompanyNombre,
    string CompanyColors,
    int PlanId,
    string Cif,
    string DireccionFiscal,
    string Sector
) : IRequest<IResult>;

public record RegisterOwnerResponse(
    string Message,
    string UserNombre,
    string Jwt,
    string RefreshToken
);