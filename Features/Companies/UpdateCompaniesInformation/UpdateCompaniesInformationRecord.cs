using MediatR;

namespace evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;

public record UpdateCompaniesInformationRecord(
    string Nombre,
    string LogoUrl,
    string Colors,
    string Cif,
    string DireccionFiscal,
    string Sector
) : IRequest<IResult>;