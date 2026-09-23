using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GenerateSubmissions;

public class GenerateSubmissionsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_GenerateSubmissions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/evaluation-cycles/1/generate-submissions", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_GenerateSubmissions_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsync("/evaluation-cycles/1/generate-submissions", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_GenerateSubmissions_AsEmployee_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com", rol: AppRoles.Employee);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(employee, company);

        var response = await client.PostAsync("/evaluation-cycles/1/generate-submissions", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_GenerateSubmissions_AsRrhh_CreatesSubmissions()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var rrhh = TestDataFactory.CreateUser(company, email: "rrhh@example.com", rol: AppRoles.Rrhh);
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");

        int cycleId;
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(rrhh, employee);
            await db.SaveChangesAsync();

            var question = new Question { Texto = "How are you?", Tipo = QuestionType.Escala1a5, Orden = 1 };
            var template = new Template
            {
                Titulo = "Template",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddDays(30),
                EmpresaID = company.Id,
                Empresa = company,
                UsuariosAsignados = [employee],
                Preguntas = [question],
            };
            var cycle = new EvaluationCycle
            {
                Nombre = "Cycle",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddDays(30),
                EmpresaID = company.Id,
                Empresa = company,
                Templates = [template],
            };
            db.EvaluationCycles.Add(cycle);
            await db.SaveChangesAsync();
            cycleId = cycle.Id;
        }

        var client = CreateAuthenticatedClient(rrhh, company);

        var response = await client.PostAsync($"/evaluation-cycles/{cycleId}/generate-submissions", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.EvaluationSubmissions.CountAsync()).Should().Be(1);
    }
}
