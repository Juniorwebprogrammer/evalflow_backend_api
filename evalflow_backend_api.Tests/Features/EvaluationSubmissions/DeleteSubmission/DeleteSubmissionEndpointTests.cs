using System.Net;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.EvaluationSubmissions.DeleteSubmission;

public class DeleteSubmissionEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Delete_Submission_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/evaluation-submissions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Submission_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.DeleteAsync("/evaluation-submissions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Submission_AsEmployee_ReturnsForbidden()
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

        var response = await client.DeleteAsync("/evaluation-submissions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Submission_AsOwner_DeletesSubmission()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var owner = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        var evaluated = TestDataFactory.CreateUser(company, email: "evaluated@example.com");
        var respondent = TestDataFactory.CreateUser(company, email: "respondent@example.com");

        int submissionId;
        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(owner, evaluated, respondent);
            await db.SaveChangesAsync();

            var template = new Template { Titulo = "Template", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company };
            var cycle = new EvaluationCycle { Nombre = "Cycle", FechaInicio = DateTime.UtcNow, FechaFin = DateTime.UtcNow.AddDays(30), EmpresaID = company.Id, Empresa = company };
            var submission = new EvaluationSubmission { Cycle = cycle, Template = template, EvaluatedUser = evaluated, RespondentUser = respondent };
            db.EvaluationSubmissions.Add(submission);
            await db.SaveChangesAsync();
            submissionId = submission.Id;
        }

        var client = CreateAuthenticatedClient(owner, company);

        var response = await client.DeleteAsync($"/evaluation-submissions/{submissionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.EvaluationSubmissions.FindAsync(submissionId)).Should().BeNull();
    }
}
