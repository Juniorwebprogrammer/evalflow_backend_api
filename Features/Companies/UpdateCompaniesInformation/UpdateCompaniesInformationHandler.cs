using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;

public class UpdateCompaniesInformationHandler(AppDbContext dbContext, ICurrentUserService currentUserService) : IRequestHandler<UpdateCompaniesInformationRecord, IResult>
{
    public async Task<IResult> Handle(UpdateCompaniesInformationRecord request, CancellationToken cancellationToken)
    {
        var identificationId = currentUserService.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(identificationId)) return Results.Unauthorized();

        var company = await FetchCompanyAsync(identificationId, cancellationToken);
        if (company is null) return Results.NotFound("Company not found");
        
        await ApplyUpdatesAndSaveAsync(company, request, cancellationToken);

        return Results.Ok(new { Message = "Company information updated" });
    }

    private async Task<Company?> FetchCompanyAsync(string identificationId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(company => company.IdentificationId == identificationId,
            cancellationToken);
    }

    private async Task ApplyUpdatesAndSaveAsync(Company company, UpdateCompaniesInformationRecord request,
        CancellationToken cancellationToken)
    {
        company.Nombre = request.Nombre;
        company.LogoUrl = request.LogoUrl;
        company.Colors = request.Colors;
        company.Cif = request.Cif;
        company.DireccionFiscal = request.DireccionFiscal;
        company.Sector = request.Sector;

        dbContext.Companies.Update(company);
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}