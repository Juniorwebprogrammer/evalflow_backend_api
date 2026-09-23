using evalflow_backend_api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Companies.GetByIdentificationId;

public class GetCompanyByIdentificationIdHandler(AppDbContext dbContext) : IRequestHandler<GetCompanyByIdentificationIdRecord, CompanyResponse?>
{
    public async Task<CompanyResponse?> Handle(GetCompanyByIdentificationIdRecord request, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .AsNoTracking()
            .Where(c => c.IdentificationId == request.IdentificationId)
            .Select(c => new CompanyResponse(c.Nombre, c.LogoUrl, c.Colors, c.IdentificationId, c.PlanId, c.Cif, c.DireccionFiscal, c.Sector))
            .FirstOrDefaultAsync(cancellationToken);
    }
}