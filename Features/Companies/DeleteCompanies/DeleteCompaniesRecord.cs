using MediatR;

namespace evalflow_backend_api.Features.Companies.DeleteCompanies;

/// <summary>Confirmed by typing the company name.</summary>
public record DeleteCompaniesRecord(string CompanyName) : IRequest<IResult>;
