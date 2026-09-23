using System.Net;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.FavoriteLists.ToggleTemplateInList;

public class ToggleTemplateInListEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_ToggleTemplateInList_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsync("/favorite-lists/1/templates/1/toggle", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ToggleTemplateInList_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsync("/favorite-lists/1/templates/1/toggle", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ToggleTemplateInList_WithValidJwt_AddsTemplateToOwnList()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");

        TemplateFavoriteList list;
        Template template;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            template = new Template
            {
                Titulo = "Evaluación",
                FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FechaFin = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EmpresaID = company.Id,
            };
            list = new TemplateFavoriteList { Nombre = "Favoritos", UserId = user.Id };
            db.Templates.Add(template);
            db.TemplateFavoriteLists.Add(list);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsync($"/favorite-lists/{list.Id}/templates/{template.Id}/toggle", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var refreshed = await verifyDb.TemplateFavoriteLists.Include(l => l.Templates).SingleAsync(l => l.Id == list.Id);
        refreshed.Templates.Should().ContainSingle(t => t.Id == template.Id);
    }
}
