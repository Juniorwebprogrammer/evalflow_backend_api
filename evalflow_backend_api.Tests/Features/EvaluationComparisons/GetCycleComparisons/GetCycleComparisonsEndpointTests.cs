using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Security.Encryption;
using evalflow_backend_api.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace evalflow_backend_api.Tests.Features.EvaluationComparisons.GetCycleComparisons;

public class GetCycleComparisonsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_Comparisons_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/evaluation-cycles/1/comparisons");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Comparisons_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/evaluation-cycles/1/comparisons");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Comparisons_WithEmployeeRole_ReturnsForbidden()
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

        var response = await client.GetAsync("/evaluation-cycles/1/comparisons");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Comparisons_WithRrhhRole_DecryptsAndComparesAnswers()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var rrhh = TestDataFactory.CreateUser(company, email: "rrhh@example.com", rol: AppRoles.Rrhh);
        EvaluationCycle cycle;
        User employee;

        using (var scope = Factory.Services.CreateScope())
        await using (var db = Factory.CreateDbContext())
        {
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            db.Companies.Add(company);
            db.Users.Add(rrhh);
            await db.SaveChangesAsync();

            cycle = new EvaluationCycle
            {
                Nombre = "Q1 Review",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
                TipoEvaluación = EvaluationType.Evaluacion360,
            };
            var template = new Template
            {
                Titulo = "Desempeño",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            var manager = TestDataFactory.CreateUser(company, email: "manager@example.com");
            db.EvaluationCycles.Add(cycle);
            db.Templates.Add(template);
            db.Users.Add(manager);
            await db.SaveChangesAsync();

            employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
            employee.SuperiorId = manager.Id;
            var question = new Question { Texto = "Cumple plazos", Tipo = QuestionType.Estrellas, Orden = 1, TemplateId = template.Id };
            db.Users.Add(employee);
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            EvaluationSubmission Completed(User respondent, string raw) => new()
            {
                EvaluationCycleId = cycle.Id,
                TemplateId = template.Id,
                EvaluatedUserId = employee.Id,
                RespondentUserId = respondent.Id,
                IsCompleted = true,
                SubmittedAt = DateTime.UtcNow,
                Answers = [new Answer { QuestionId = question.Id, EncryptedPayload = encryption.Encrypt(raw) }],
            };

            db.EvaluationSubmissions.AddRange(Completed(employee, "2"), Completed(manager, "4"));
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(rrhh, company);

        var response = await client.GetAsync($"/evaluation-cycles/{cycle.Id}/comparisons?evaluatedUserId={employee.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var comparison = json.GetProperty("comparisons").EnumerateArray().Should().ContainSingle().Subject;
        comparison.GetProperty("isComparable").GetBoolean().Should().BeTrue();

        var answer = comparison.GetProperty("questions")[0];
        answer.GetProperty("selfValue").GetInt32().Should().Be(2);
        answer.GetProperty("managerValue").GetInt32().Should().Be(4);
        answer.GetProperty("level").GetString().Should().Be("Desequilibrio");
        answer.GetProperty("direction").GetString().Should().Be("Infravaloracion");
    }

    [Fact]
    public async Task Get_Comparisons_WithUnknownCycle_ReturnsNotFound()
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

        var response = await client.GetAsync("/evaluation-cycles/999/comparisons");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
