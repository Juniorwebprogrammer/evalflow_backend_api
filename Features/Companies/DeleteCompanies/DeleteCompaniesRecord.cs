using MediatR;

namespace evalflow_backend_api.Features.Companies.DeleteCompanies;

public record DeleteCompaniesRecord(string Password) : IRequest<IResult>;