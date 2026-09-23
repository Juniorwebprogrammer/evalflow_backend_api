using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Infrastructure.Security.PasswordHasher;
using MediatR;

namespace evalflow_backend_api.Features.Profile.DeleteAccount;

public class DeleteAccountHandler(AppDbContext dbContext, ICurrentUserService currentUserService, IPasswordHasser passwordHasser) : IRequestHandler<DeleteAccountRecord, IResult>
{
    public async Task<IResult> Handle(DeleteAccountRecord request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
        
        var user = await GetUserEntityAsync(userId, cancellationToken);
        if (user is null) return Results.NotFound("User not found");

        if (!passwordHasser.Verify(request.Password, user.PasswordHash))
        {
            return Results.BadRequest("The password is incorrect. We can't delete your account");
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