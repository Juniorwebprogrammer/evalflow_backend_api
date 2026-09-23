using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Questions.CreateQuestion;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Questions.CreateQuestion;

public class CreateQuestionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_Questions_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync(
            "/templates/1/questions",
            new CreateQuestionBody("Question text", QuestionType.Escala1a5, "General", null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Questions_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(
            "/templates/1/questions",
            new CreateQuestionBody("Question text", QuestionType.Escala1a5, "General", null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Questions_WithValidOwnerJwt_CreatesQuestion()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        Template template;

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
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.PostAsJsonAsync(
            $"/templates/{template.Id}/questions",
            new CreateQuestionBody("How would you rate your teammate?", QuestionType.Escala1a5, "Teamwork", null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Questions.SingleAsync()).Texto.Should().Be("How would you rate your teammate?");
    }
}
