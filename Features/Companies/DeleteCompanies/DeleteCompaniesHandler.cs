using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Companies.DeleteCompanies;

public class DeleteCompaniesHandler(AppDbContext dbContext, ICurrentUserService currentUserService, IPasswordHasser passwordHasser) : IRequestHandler<DeleteCompaniesRecord, IResult>
{
    public async Task<IResult> Handle(DeleteCompaniesRecord request, CancellationToken cancellationToken)
    {
        var identificationId = currentUserService.GetIdentificationId();
        var userIdString = currentUserService.GetUserId();
        if (string.IsNullOrWhiteSpace(identificationId) || string.IsNullOrWhiteSpace(userIdString))
        {
            return Results.Unauthorized();
        }

        var user = await FetchUserAsync(userIdString, cancellationToken);
        if (user is null || !passwordHasser.Verify(request.Password, user.PasswordHash))
        {
            return Results.BadRequest("Incorrect password, canceled operation");
        }
        
        var company = await FetchCompanyAsync(identificationId, cancellationToken);
        if (company is null) return Results.NotFound("Company not found.");
        
        await DeleteCompanyAndSaveAsync(company, cancellationToken);

        return Results.Ok(new { Message = "Company deleted of the system" });
    }

    private async Task<User?> FetchUserAsync(string userIdString, CancellationToken cancellationToken)
    {
        if (int.TryParse(userIdString, out int userId))
        {
            return await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        }

        return null;
    }
    
    private async Task<Domain.Entities.Company?> FetchCompanyAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Companies
            .FirstOrDefaultAsync(c => c.IdentificationId == tenantId, cancellationToken);
    }
    
    private async Task DeleteCompanyAndSaveAsync(Domain.Entities.Company company, CancellationToken cancellationToken)
    {
        var userIds = await dbContext.Users
            .Where(u => u.EmpresaID == company.Id)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var cycleIds = await dbContext.EvaluationCycles
            .Where(c => c.EmpresaID == company.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        await RemoveEvaluationDataAsync(userIds, cycleIds, cancellationToken);
        await RemoveUsersAsync(company.Id, cancellationToken);

        dbContext.Companies.Remove(company);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveEvaluationDataAsync(List<int> userIds, List<int> cycleIds, CancellationToken cancellationToken)
    {
        dbContext.EvaluationResults.RemoveRange(await dbContext.EvaluationResults
            .Where(r => cycleIds.Contains(r.EvaluationCycleId)
                || userIds.Contains(r.EvaluatedUserId)
                || userIds.Contains(r.CompletedByUserId))
            .ToListAsync(cancellationToken));

        dbContext.DiscrepancyAcceptances.RemoveRange(await dbContext.DiscrepancyAcceptances
            .Where(a => cycleIds.Contains(a.EvaluationCycleId)
                || userIds.Contains(a.EvaluatedUserId)
                || userIds.Contains(a.AcceptedByUserId))
            .ToListAsync(cancellationToken));

        dbContext.ClarificationRequests.RemoveRange(await dbContext.ClarificationRequests
            .Where(c => cycleIds.Contains(c.EvaluationCycleId)
                || userIds.Contains(c.EvaluatedUserId)
                || userIds.Contains(c.ManagerUserId)
                || userIds.Contains(c.RequestedByUserId))
            .ToListAsync(cancellationToken));

        dbContext.EvaluationSubmissions.RemoveRange(await dbContext.EvaluationSubmissions
            .Where(s => cycleIds.Contains(s.EvaluationCycleId)
                || userIds.Contains(s.EvaluatedUserId)
                || userIds.Contains(s.RespondentUserId))
            .ToListAsync(cancellationToken));
    }

    private async Task RemoveUsersAsync(int companyId, CancellationToken cancellationToken)
    {
        dbContext.Users.RemoveRange(await dbContext.Users
            .Where(u => u.EmpresaID == companyId)
            .ToListAsync(cancellationToken));
    }
}