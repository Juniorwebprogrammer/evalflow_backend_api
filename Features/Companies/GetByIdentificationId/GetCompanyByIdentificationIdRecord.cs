using MediatR;

namespace evalflow_backend_api.Features.Companies.GetByIdentificationId;

public record GetCompanyByIdentificationIdRecord(
    string IdentificationId
) : IRequest<CompanyResponse?>;

public record CompanyResponse(
    string Nombre,
    string? LogoURL,
    string Colors,
    string IdentificationId,
    int PlanId,
    string Cif,
    string? DireccionFiscal,
    string? Sector
);