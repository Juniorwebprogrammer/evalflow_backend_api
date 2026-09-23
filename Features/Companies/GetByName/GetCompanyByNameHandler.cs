using evalflow_backend_api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Companies.GetByName;

public class GetCompanyByNameHandler(AppDbContext dbContext) : IRequestHandler<GetCompanyByNameQuery, CompanyResponse?>
{
    public async Task<CompanyResponse?> Handle(GetCompanyByNameQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .AsNoTracking()
            .Where(c => c.Nombre == request.Nombre)
            .Select(c => new CompanyResponse(c.Nombre, c.LogoUrl, c.Colors, c.IdentificationId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}