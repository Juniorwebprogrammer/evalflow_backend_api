using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.JobPosittions.CreateJobPosition;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.JobPositions.CreateJobPosition;

public class CreateJobPositionHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CreateJobPositionRecord, IResult>
{
    public async Task<IResult> Handle(CreateJobPositionRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) return Results.Unauthorized();

        var jobPosition = await CreateAndSaveJobPositionAsync(request, company.Id, cancellationToken);

        return GenerateSuccessResponse(jobPosition.Id);
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<JobPosition> CreateAndSaveJobPositionAsync(CreateJobPositionRecord request, int companyId, CancellationToken cancellationToken)
    {
        var jobPosition = new JobPosition
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            EmpresaID = companyId
        };

        dbContext.JobPositions.Add(jobPosition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return jobPosition;
    }

    private static IResult GenerateSuccessResponse(int id)
    {
        return Results.Created($"/job-positions/{id}", new { Message = "Cargo creado con éxito.", Id = id });
    }
}