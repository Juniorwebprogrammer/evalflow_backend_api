using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Questions.UpdateQuestion;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Questions.UpdateQuestion;

public class UpdateQuestionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_Question_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync(
            "/templates/1/questions/1",
            new UpdateQuestionBody("Updated text", "General", QuestionType.Escala1a5, null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Question_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync(
            "/templates/1/questions/1",
            new UpdateQuestionBody("Updated text", "General", QuestionType.Escala1a5, null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Question_WithValidOwnerJwt_UpdatesQuestion()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        Template template;
        Question question;

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Template/Question FKs need the company/template row ids EF just generated, so they
            // must be constructed after that save, not inline with it.
            template = new Template
            {
                Titulo = "Peer Review",
                FechaInicio = DateTime.UtcNow,
                FechaFin = DateTime.UtcNow.AddMonths(1),
                EmpresaID = company.Id,
            };
            db.Templates.Add(template);
            await db.SaveChangesAsync();

            question = new Question
            {
                Texto = "Old text",
                Tipo = QuestionType.Escala1a5,
                Orden = 1,
                TemplateId = template.Id,
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PutAsJsonAsync(
            $"/templates/{template.Id}/questions/{question.Id}",
            new UpdateQuestionBody("New text", "Teamwork", QuestionType.Estrellas, null, 2));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var updated = await verifyDb.Questions.FindAsync(question.Id);
        updated!.Texto.Should().Be("New text");
        updated.Topic.Should().Be("Teamwork");
    }
}
