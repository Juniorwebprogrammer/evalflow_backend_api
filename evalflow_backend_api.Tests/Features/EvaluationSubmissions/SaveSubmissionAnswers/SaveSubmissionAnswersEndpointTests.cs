using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.SaveSubmissionAnswers;

public class SaveSubmissionAnswersEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_SubmissionAnswers_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/evaluation-submissions/1/answers", new { Answers = new object[0] });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_SubmissionAnswers_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/evaluation-submissions/1/answers", new { Answers = new object[0] });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_SubmissionAnswers_AsRespondent_SavesAnswersAndCompletesSubmission()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");

        int submissionId;
        int questionId;
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(respondent, evaluated);
            await db.SaveChangesAsync();

            var question = new Question { Texto = "Q1", Tipo = QuestionType.Escala1a5, Orden = 1 };
            var template = new Template { Titulo = "Template", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company, Preguntas = [question] };
            var cycle = new EvaluationCycle { Nombre = "Cycle", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company };
            db.Templates.Add(template);
            db.EvaluationCycles.Add(cycle);

            var submission = new EvaluationSubmission
            {
                Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false,
                Answers = [new Answer { Question = question, EncryptedPayload = string.Empty }],
            };
            db.EvaluationSubmissions.Add(submission);
            await db.SaveChangesAsync();
            submissionId = submission.Id;
            questionId = question.Id;
        }

        var client = CreateAuthenticatedClient(respondent, company);

        var response = await client.PutAsJsonAsync($"/evaluation-submissions/{submissionId}/answers", new
        {
            Answers = new[] { new { QuestionId = questionId, RawPayload = "my answer" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.EvaluationSubmissions.Include(s => s.Answers).SingleAsync(s => s.Id == submissionId);
        updated.IsCompleted.Should().BeTrue();
        updated.Answers.Single().EncryptedPayload.Should().NotBeNullOrEmpty();
        updated.Answers.Single().EncryptedPayload.Should().NotBe("my answer");
    }

    [Fact]
    public async Task Put_SubmissionAnswers_NotBelongingToCaller_ReturnsNotFound()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");
        var requester = TestDataFactory.CreateUser(company, email: "requester@example.com");
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");

        int submissionId;
        int questionId;
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(respondent, requester, evaluated);
            await db.SaveChangesAsync();

            var question = new Question { Texto = "Q1", Tipo = QuestionType.Escala1a5, Orden = 1 };
            var template = new Template { Titulo = "Template", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company, Preguntas = [question] };
            var cycle = new EvaluationCycle { Nombre = "Cycle", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company };
            db.Templates.Add(template);
            db.EvaluationCycles.Add(cycle);

            var submission = new EvaluationSubmission
            {
                Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent, IsCompleted = false,
                Answers = [new Answer { Question = question, EncryptedPayload = string.Empty }],
            };
            db.EvaluationSubmissions.Add(submission);
            await db.SaveChangesAsync();
            submissionId = submission.Id;
            questionId = question.Id;
        }

        var client = CreateAuthenticatedClient(requester, company);

        var response = await client.PutAsJsonAsync($"/evaluation-submissions/{submissionId}/answers", new
        {
            Answers = new[] { new { QuestionId = questionId, RawPayload = "my answer" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
