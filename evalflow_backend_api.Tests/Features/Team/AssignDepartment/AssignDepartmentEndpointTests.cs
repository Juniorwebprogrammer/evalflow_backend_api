using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Team.AssignDepartment;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Team.AssignDepartment;

public class AssignDepartmentEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Put_AssignDepartment_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PutAsJsonAsync("/team/1/department", new DepartmentAssignBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_AssignDepartment_WithoutJwt_ReturnsUnauthorized()
    {
        var response = await Client.PutAsJsonAsync("/team/1/department", new DepartmentAssignBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_AssignDepartment_WithEmployeeRole_ReturnsForbidden()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "employee-caller@example.com", rol: AppRoles.Employee);
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(caller, employee);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/department", new DepartmentAssignBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_AssignDepartment_WithOwnerRole_AssignsDepartmentAndReturnsOk()
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var caller = TestDataFactory.CreateUser(company, email: "owner@example.com", rol: AppRoles.Owner);
        var employee = TestDataFactory.CreateUser(company, email: "employee@example.com");
        var department = new Department { Nombre = "Ingeniería", EmpresaID = company.Id, Empresa = company };

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.AddRange(caller, employee);
            db.Departments.Add(department);
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(caller, company);

        var response = await client.PutAsJsonAsync($"/team/{employee.Id}/department", new DepartmentAssignBody(department.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        (await verifyDb.Users.SingleAsync(u => u.Id == employee.Id)).DepartamentoId.Should().Be(department.Id);
    }
}
