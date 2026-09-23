using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using QuestionDtoType = evalflow_backend_api.Features.Questions.QuestionDto.QuestionDto;

namespace evalflow_backend_api.Tests.Features.Questions.GetQuestionsByTemplate;

public class GetQuestionsByTemplateEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_QuestionsByTemplate_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.GetAsync("/templates/1/questions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_QuestionsByTemplate_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/templates/1/questions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_QuestionsByTemplate_WithValidJwt_ReturnsQuestions()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "employee@example.com");
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

            question = new Question { Texto = "How is it going?", Tipo = QuestionType.Escala1a5, Orden = 1, TemplateId = template.Id };
            db.Questions.Add(question);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(user, company);

        var response = await client.GetAsync($"/templates/{template.Id}/questions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<QuestionDtoType>>();
        payload.Should().ContainSingle(q => q.Texto == "How is it going?");
    }
}
