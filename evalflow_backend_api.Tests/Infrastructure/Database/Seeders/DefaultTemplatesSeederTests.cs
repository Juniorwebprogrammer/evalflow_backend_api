using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database.Seeders;
using evalflow_backend_api.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Tests.Infrastructure.Database.Seeders;

public class DefaultTemplatesSeederTests
{
    // ----- SeedForCompanyAsync (integration with the DbContext) -----

    [Fact]
    public async Task SeedForCompanyAsync_NewCompany_CreatesThe180_360AndAutoTemplates()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await DefaultTemplatesSeeder.SeedForCompanyAsync(db, company.Id, CancellationToken.None);

        var titles = await db.Templates.Where(t => t.EmpresaID == company.Id).Select(t => t.Titulo).ToListAsync();
        titles.Should().HaveCount(3);
        titles.Should().Contain("Plantilla Estándar 180° (Solo Mánager)");
        titles.Should().Contain("Plantilla Integral 360°");
        titles.Should().Contain("Plantilla de Autoevaluación");
    }

    [Fact]
    public async Task SeedForCompanyAsync_NewCompany_PersistsQuestionsForEachTemplate()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await DefaultTemplatesSeeder.SeedForCompanyAsync(db, company.Id, CancellationToken.None);

        var templates = await db.Templates.Include(t => t.Preguntas).Where(t => t.EmpresaID == company.Id).ToListAsync();
        templates.Should().AllSatisfy(t => t.Preguntas.Should().NotBeEmpty());
        templates.Sum(t => t.Preguntas.Count).Should().Be(8); // 2 (180) + 3 (360) + 3 (auto)
    }

    [Fact]
    public async Task SeedForCompanyAsync_CompanyAlreadyHasTemplates_DoesNotDuplicateThem()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await DefaultTemplatesSeeder.SeedForCompanyAsync(db, company.Id, CancellationToken.None);
        await DefaultTemplatesSeeder.SeedForCompanyAsync(db, company.Id, CancellationToken.None);

        (await db.Templates.CountAsync(t => t.EmpresaID == company.Id)).Should().Be(3);
    }

    [Fact]
    public async Task SeedForCompanyAsync_CompanyWithPreExistingCustomTemplate_SkipsSeedingDefaults()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var company = TestDataFactory.CreateCompany();
        db.Companies.Add(company);
        db.Templates.Add(new Template { EmpresaID = company.Id, Titulo = "Plantilla a medida del cliente" });
        await db.SaveChangesAsync();

        await DefaultTemplatesSeeder.SeedForCompanyAsync(db, company.Id, CancellationToken.None);

        var templates = await db.Templates.Where(t => t.EmpresaID == company.Id).ToListAsync();
        templates.Should().ContainSingle().Which.Titulo.Should().Be("Plantilla a medida del cliente");
    }

    [Fact]
    public async Task SeedForCompanyAsync_MultipleCompanies_OnlySeedsTheTargetCompany()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var seededCompany = TestDataFactory.CreateCompany(nombre: "Seeded Co");
        var untouchedCompany = TestDataFactory.CreateCompany(nombre: "Untouched Co");
        db.Companies.AddRange(seededCompany, untouchedCompany);
        await db.SaveChangesAsync();

        await DefaultTemplatesSeeder.SeedForCompanyAsync(db, seededCompany.Id, CancellationToken.None);

        (await db.Templates.CountAsync(t => t.EmpresaID == seededCompany.Id)).Should().Be(3);
        (await db.Templates.CountAsync(t => t.EmpresaID == untouchedCompany.Id)).Should().Be(0);
    }

    // ----- BuildEvaluacion180Template -----

    [Fact]
    public void BuildEvaluacion180Template_ReturnsTemplateScopedToCompanyWithExpectedQuestions()
    {
        var template = DefaultTemplatesSeeder.BuildEvaluacion180Template(companyId: 42);

        template.EmpresaID.Should().Be(42);
        template.Titulo.Should().Be("Plantilla Estándar 180° (Solo Mánager)");
        template.Preguntas.Should().HaveCount(2);
        template.Preguntas.Select(q => q.Orden).Should().Equal(1, 2);
    }

    // ----- BuildEvaluacion360Template -----

    [Fact]
    public void BuildEvaluacion360Template_ReturnsTemplateScopedToCompanyWithExpectedQuestions()
    {
        var template = DefaultTemplatesSeeder.BuildEvaluacion360Template(companyId: 42);

        template.EmpresaID.Should().Be(42);
        template.Titulo.Should().Be("Plantilla Integral 360°");
        template.Preguntas.Should().HaveCount(3);
        template.Preguntas.Select(q => q.Orden).Should().Equal(1, 2, 3);
    }

    // ----- BuildAutoEvaluacionTemplate -----

    [Fact]
    public void BuildAutoEvaluacionTemplate_ReturnsTemplateScopedToCompanyWithExpectedQuestions()
    {
        var template = DefaultTemplatesSeeder.BuildAutoEvaluacionTemplate(companyId: 42);

        template.EmpresaID.Should().Be(42);
        template.Titulo.Should().Be("Plantilla de Autoevaluación");
        template.Preguntas.Should().HaveCount(3);
        template.Preguntas.Select(q => q.Orden).Should().Equal(1, 2, 3);
    }
}
