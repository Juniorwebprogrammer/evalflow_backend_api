using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GetCycleSubmissions;

public class GetCycleSubmissionsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_CycleSubmissions_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/evaluation-cycles/1/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_CycleSubmissions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/evaluation-cycles/1/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_CycleSubmissions_WithEmployeeRole_ReturnsForbidden()
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

        var response = await client.GetAsync("/evaluation-cycles/1/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_CycleSubmissions_WithOwnerRole_ReturnsSubmissions()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        EvaluationCycle cycle;
        Template template;
        User evaluated;
        User respondent;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(owner);
            await db.SaveChangesAsync();

            // FKs below need the company row id EF just generated above.
            cycle = new EvaluationCycle
            {
                Nombre = "Q1 Review",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            template = new Template
            {
                Titulo = "Peer Review",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
            respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
            db.EvaluationCycles.Add(cycle);
            db.Templates.Add(template);
            db.Users.AddRange(evaluated, respondent);
            await db.SaveChangesAsync();

            db.EvaluationSubmissions.Add(new EvaluationSubmission
            {
                EvaluationCycleId = cycle.Id,
                TemplateId = template.Id,
                EvaluatedUserId = evaluated.Id,
                RespondentUserId = respondent.Id,
                IsCompleted = false,
            });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.GetAsync($"/evaluation-cycles/{cycle.Id}/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<CycleSubmissionDto>>();
        payload.Should().ContainSingle(s => s.RespondentUserId == respondent.Id);
    }

    [Fact]
    public async Task Get_CycleSubmissions_WithUnknownCycle_ReturnsNotFound()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(owner);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.GetAsync("/evaluation-cycles/999/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
