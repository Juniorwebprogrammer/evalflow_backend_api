using MediatR;

namespace evalflow_backend_api.Features.Onboarding.RegisterOwner;

/// <summary>
/// Creates a company and its owner. The owner is the Vexxen user of <see cref="VexxenAccessToken"/> (name and email
/// come from Vexxen) and the plan is the one they bought in the Vexxen catalog: they must have one (or a trial).
/// </summary>
public record RegisterOwnerRecord(
    string VexxenAccessToken,
    string CompanyNombre,
    string CompanyColors,
    string Cif,
    string DireccionFiscal,
    string Sector
) : IRequest<IResult>;

public record RegisterOwnerResponse(
    string Message,
    string UserNombre,
    string Jwt,
    string IdentificationId,
    int PlanId,
    string PlanStatus
);
