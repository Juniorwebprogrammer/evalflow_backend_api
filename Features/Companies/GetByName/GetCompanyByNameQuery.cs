using evalflow_backend_api.Domain.Entities;
using MediatR;

namespace evalflow_backend_api.Features.Companies.GetByName;

public record GetCompanyByNameQuery(
    string Nombre
) : IRequest<CompanyResponse?>;

public record CompanyResponse(
    string Nombre,
    string? LogoURL,
    string Colors,
    string IdentificationId
); 