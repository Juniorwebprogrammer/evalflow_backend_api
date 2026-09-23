using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Team.GetSubordinates;

public class GetSubordinatesHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<GetSubordinatesRecord, IResult>
{
    public async Task<IResult> Handle(GetSubordinatesRecord request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetIdentificationId();
        if (string.IsNullOrWhiteSpace(tenantId)) 
        {
            return Results.Unauthorized();
        }

        var company = await GetCompanyAsync(tenantId, cancellationToken);
        if (company is null) 
        {
            return Results.Unauthorized();
        }

        var subordinates = await FetchSubordinatesAsync(request.UserId, company.Id, cancellationToken);

        return GenerateSuccessResponse(subordinates);
    }

    private async Task<Company?> GetCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<List<SubordinateDto>> FetchSubordinatesAsync(int userId, int companyId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking() 
            .Where(u => u.SuperiorId == userId && u.EmpresaID == companyId)
            .Select(u => new SubordinateDto(
                u.Id, 
                u.Nombre, 
                u.Apellidos, 
                u.Email, 
                u.Rol,
                u.Cargo!.Nombre,
                u.Activo
            ))
            .ToListAsync(cancellationToken);
    }

    private static IResult GenerateSuccessResponse(List<SubordinateDto> subordinates)
    {
        return Results.Ok(subordinates);
    }
}