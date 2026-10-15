using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Admin;

public class CreateAdministratorCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreatesThe_InternalCompany_AndAPendingAdministrator()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var result = await new CreateAdministratorCommand(db).ExecuteAsync(" Admin@EvalFlow.com ", "Ada", "Lovelace", CancellationToken.None, Now);

        result.Ok.Should().BeTrue();
        result.InvitationExpiresAt.Should().Be(Now.AddDays(30));
        var admin = await db.Users.Include(u => u.Empresa).SingleAsync();
        admin.Email.Should().Be("admin@evalflow.com");
        admin.Rol.Should().Be(AppRoles.Administrator);
        admin.VexxenUserId.Should().BeNull("it gets linked on the first Vexxen sign-in, like an invitation");
        admin.TokenInvitacion.Should().NotBeNullOrEmpty();
        admin.Empresa!.IdentificationId.Should().Be(CreateAdministratorCommand.InternalCompanyId);
        admin.Empresa.PlanId.Should().Be(PlanCatalog.Enterprise);
        admin.Empresa.PlanManual.Should().BeTrue();
        admin.Empresa.PlanStatus.Should().Be(PlanStatuses.Active);
    }

    [Fact]
    public async Task ASecondAdministrator_ReusesTheInternalCompany()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var command = new CreateAdministratorCommand(db);

        await command.ExecuteAsync("one@evalflow.com", "One", "Admin", CancellationToken.None, Now);
        await command.ExecuteAsync("two@evalflow.com", "Two", "Admin", CancellationToken.None, Now);

        (await db.Companies.CountAsync()).Should().Be(1);
        (await db.Users.CountAsync(u => u.Rol == AppRoles.Administrator)).Should().Be(2);
    }

    [Fact]
    public async Task RunningItAgain_RenewsAPendingInvitation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var command = new CreateAdministratorCommand(db);
        await command.ExecuteAsync("admin@evalflow.com", "Ada", "Lovelace", CancellationToken.None, Now);
        var firstToken = (await db.Users.SingleAsync()).TokenInvitacion;

        var later = Now.AddDays(40);
        var result = await command.ExecuteAsync("admin@evalflow.com", "Ada", "Lovelace", CancellationToken.None, later);

        result.InvitationExpiresAt.Should().Be(later.AddDays(30));
        (await db.Users.SingleAsync()).TokenInvitacion.Should().NotBe(firstToken);
    }

    [Fact]
    public async Task AnAdministratorAlreadyLinked_IsLeftAlone()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var command = new CreateAdministratorCommand(db);
        await command.ExecuteAsync("admin@evalflow.com", "Ada", "Lovelace", CancellationToken.None, Now);
        var admin = await db.Users.SingleAsync();
        admin.VexxenUserId = "vx-admin";
        admin.TokenInvitacion = null;
        await db.SaveChangesAsync();

        var result = await command.ExecuteAsync("admin@evalflow.com", "Ada", "Lovelace", CancellationToken.None, Now);

        result.Ok.Should().BeTrue();
        (await db.Users.SingleAsync()).TokenInvitacion.Should().BeNull();
    }

    [Fact]
    public async Task AnEmailFromACustomerCompany_IsRejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        db.Users.Add(TestDataFactory.CreateUser(company, email: "ana@acme.com", rol: AppRoles.Owner));
        await db.SaveChangesAsync();

        var result = await new CreateAdministratorCommand(db).ExecuteAsync("ana@acme.com", "Ana", "Pérez", CancellationToken.None, Now);

        result.Ok.Should().BeFalse();
        (await db.Users.SingleAsync()).Rol.Should().Be(AppRoles.Owner);
    }
}
