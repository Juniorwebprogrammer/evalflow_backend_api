using System.Net;
using System.Net.Http.Json;
using evalflow_backend_api.Features.Onboarding.RegisterOwner;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Features.Onboarding.RegisterOwner;

public class RegisterOwnerEndpointTests : IntegrationTestBase
{
    private static RegisterOwnerRecord ValidRequest(string email = "owner@example.com") => new(
        UserNombre: "Ada",
        Apellidos: "Lovelace",
        Email: email,
        Password: "SecurePass1",
        CompanyNombre: "Acme Corp",
        CompanyColors: "#FFFFFF",
        PlanId: 1,
        Cif: "B12345678",
        DireccionFiscal: "Calle Falsa 123",
        Sector: "Tech"
    );

    [Fact]
    public async Task Post_RegisterOwner_WithoutApiKey_ReturnsUnauthorized()
    {
        var client = CreateClient(includeApiKey: false);

        var response = await client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_RegisterOwner_WithInvalidEmail_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest(email: "not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_RegisterOwner_WithDuplicateEmail_ReturnsConflict()
    {
        var company = TestDataFactory.CreateCompany();
        var existingUser = TestDataFactory.CreateUser(company, email: "owner@example.com");

        await using (var db = Factory.CreateDbContext())
        {
            db.Companies.Add(company);
            db.Users.Add(existingUser);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest(email: "owner@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_RegisterOwner_WithValidRequest_CreatesCompanyAndOwner()
    {
        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RegisterOwnerResponse>();
        payload!.Jwt.Should().NotBeNullOrEmpty();

        await using var verifyDb = Factory.CreateDbContext();
        var createdUser = await verifyDb.Users.Include(u => u.Empresa).SingleAsync(u => u.Email == "owner@example.com");
        createdUser.Empresa!.Nombre.Should().Be("acme corp");

        Factory.FakeEmailService.SentEmails.Should().ContainSingle(e => e.To == "owner@example.com");
    }

    [Fact]
    public async Task Post_RegisterOwner_WithValidRequest_SeedsDefault180_360AndAutoTemplates()
    {
        var response = await Client.PostAsJsonAsync("/Onboarding/register-owner", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyDb = Factory.CreateDbContext();
        var createdUser = await verifyDb.Users.Include(u => u.Empresa).SingleAsync(u => u.Email == "owner@example.com");

        var templateTitles = await verifyDb.Templates
            .Where(t => t.EmpresaID == createdUser.Empresa!.Id)
            .Select(t => t.Titulo)
            .ToListAsync();

        templateTitles.Should().HaveCount(3);
        templateTitles.Should().Contain("Plantilla Estándar 180° (Solo Mánager)");
        templateTitles.Should().Contain("Plantilla Integral 360°");
        templateTitles.Should().Contain("Plantilla de Autoevaluación");
    }
}
