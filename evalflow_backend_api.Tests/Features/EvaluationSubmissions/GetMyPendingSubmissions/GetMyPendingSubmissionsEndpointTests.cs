using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.EvaluationSubmissions.EvaluationSubmissionsDto;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.GetMyPendingSubmissions;

public class GetMyPendingSubmissionsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_PendingSubmissions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/evaluation-submissions/pending");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_PendingSubmissions_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/evaluation-submissions/pending");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_PendingSubmissions_WithValidJwt_ReturnsOnlyCallersPendingSubmissionsOnActiveCycles()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(respondent, evaluated);
            await db.SaveChangesAsync();

            var question = new Question { Texto = "Q1", Tipo = QuestionType.Escala1a5, Orden = 1 };
            var template = new Template { Titulo = "Template", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company, Preguntas = [question] };
            var cycle = new EvaluationCycle { Nombre = "Cycle", Activo = true, FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company };
            db.Templates.Add(template);
            db.EvaluationCycles.Add(cycle);
            await db.SaveChangesAsync();

            db.EvaluationSubmissions.Add(new EvaluationSubmission
            {
                Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false,
            });
            db.EvaluationSubmissions.Add(new EvaluationSubmission
            {
                Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = true,
            });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(respondent, company);

        var response = await client.GetAsync("/evaluation-submissions/pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<PendingSubmissionDto>>();
        payload.Should().ContainSingle();
        payload![0].TemplateTitle.Should().Be("Template");
    }
}
