using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Profile.DeleteAccount;

public class DeleteAccountHandler(AppDbContext dbContext, ICurrentUserService currentUserService) : IRequestHandler<DeleteAccountRecord, IResult>
{
    public async Task<IResult> Handle(DeleteAccountRecord request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
        
        var user = await GetUserEntityAsync(userId, cancellationToken);
        if (user is null) return Results.NotFound("User not found");

        if (!string.Equals(request.ConfirmEmail?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { Code = "INVALID_CONFIRMATION", Message = "The email doesn't match your account. We can't delete it." });
        }

        // Whoever pays for EvalFlow can't leave the company without it: delete the company instead.
        var paysForCompany = user.VexxenUserId is not null
            && await dbContext.Companies.AnyAsync(c => c.Id == user.EmpresaID && c.BillingVexxenUserId == user.VexxenUserId, cancellationToken);
        if (paysForCompany)
        {
            return Results.Conflict(new { Code = "BILLING_OWNER", Message = "You pay for your company's EvalFlow plan, so you can't delete your account. Delete the company instead." });
        }
        
        await DeleteUserAndSaveAsync(user, cancellationToken);

        return Results.Ok(new { Message = "The account are deleted successfully" });
    }

    private async Task<User?> GetUserEntityAsync(string userIdString, CancellationToken cancellationToken)
    {
        if (int.TryParse(userIdString, out int userId))
        {
            return await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        }

        return null;
    }

    private async Task DeleteUserAndSaveAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}