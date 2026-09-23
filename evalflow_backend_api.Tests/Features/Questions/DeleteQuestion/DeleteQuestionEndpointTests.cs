using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Questions.DeleteQuestion;

public class DeleteQuestionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Delete_Question_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.DeleteAsync("/templates/1/questions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Question_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/templates/1/questions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Question_WithValidOwnerJwt_DeletesQuestion()
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

            // EmpresaID needs the company row id EF just generated above.
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
                Texto = "Question to delete",
                Tipo = QuestionType.Escala1a5,
                Orden = 1,
                TemplateId = template.Id,
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.DeleteAsync($"/templates/{template.Id}/questions/{question.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Questions.FindAsync(question.Id)).Should().BeNull();
    }
}
