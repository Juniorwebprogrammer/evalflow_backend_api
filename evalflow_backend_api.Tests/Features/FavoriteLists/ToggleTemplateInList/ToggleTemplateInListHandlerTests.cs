using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.FavoriteLists.ToggleTemplateInList;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.ToggleTemplateInList;

public class ToggleTemplateInListHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private ToggleTemplateInListHandler CreateSut(evalflow_backend_api.Infrastructure.Database.AppDbContext dbContext) =>
        new(dbContext, _currentUser.Object);

    private static Template BuildTemplate(int companyId, string titulo = "Evaluación") => new()
    {
        Titulo = titulo,
        FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaFin = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        EmpresaID = companyId,
    };

    [Fact]
    public async Task Handle_WithoutUserClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(1, 1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithoutTenantClaim_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("1");
        _currentUser.Setup(c => c.GetIdentificationId()).Returns((string?)null);

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(1, 1), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Handle_WithNonExistentList_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns("1");
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(999, 1), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithListOwnedByAnotherUser_ReturnsNotFound()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com");
        var otherUser = TestDataFactory.CreateUser(company, email: "other@example.com");
        db.Companies.Add(company);
        db.Users.AddRange(owner, otherUser);
        await db.SaveChangesAsync();

        var list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = owner.Id };
        var template = BuildTemplate(company.Id);
        db.TemplateFavoriteLists.Add(list);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(otherUser.Id.ToString());
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(list.Id, template.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Handle_WithTemplateFromAnotherCompany_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var otherCompany = TestDataFactory.CreateCompany(identificationId: "tenant-2");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.AddRange(company, otherCompany);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = user.Id };
        var foreignTemplate = BuildTemplate(otherCompany.Id);
        db.TemplateFavoriteLists.Add(list);
        db.Templates.Add(foreignTemplate);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(list.Id, foreignTemplate.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Handle_WithTemplateNotYetInList_AddsItAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = user.Id };
        var template = BuildTemplate(company.Id);
        db.TemplateFavoriteLists.Add(list);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(list.Id, template.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.TemplateFavoriteLists.Include(l => l.Templates).SingleAsync(l => l.Id == list.Id);
        refreshed.Templates.Should().ContainSingle(t => t.Id == template.Id);
    }

    [Fact]
    public async Task Handle_WithTemplateAlreadyInList_RemovesItAndReturnsOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var template = BuildTemplate(company.Id);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = user.Id };
        list.Templates.Add(template);
        db.TemplateFavoriteLists.Add(list);
        await db.SaveChangesAsync();

        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        _currentUser.Setup(c => c.GetIdentificationId()).Returns("tenant-1");

        var sut = CreateSut(db);

        var result = await sut.Handle(new ToggleTemplateInListRecord(list.Id, template.Id), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var refreshed = await db.TemplateFavoriteLists.Include(l => l.Templates).SingleAsync(l => l.Id == list.Id);
        refreshed.Templates.Should().BeEmpty();
    }
}
