using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
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
        titles.Should().Contain("Standard 180° Template (Manager Only)");
        titles.Should().Contain("Comprehensive 360° Template");
        titles.Should().Contain("Self-Assessment Template");
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
        templates.Sum(t => t.Preguntas.Count).Should().Be(34); // 11 (180) + 12 (360) + 11 (auto)
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
        template.Titulo.Should().Be("Standard 180° Template (Manager Only)");
        template.Preguntas.Should().HaveCount(11);
        template.Preguntas.Select(q => q.Orden).Should().Equal(Enumerable.Range(1, 11));
    }

    // ----- BuildEvaluacion360Template -----

    [Fact]
    public void BuildEvaluacion360Template_ReturnsTemplateScopedToCompanyWithExpectedQuestions()
    {
        var template = DefaultTemplatesSeeder.BuildEvaluacion360Template(companyId: 42);

        template.EmpresaID.Should().Be(42);
        template.Titulo.Should().Be("Comprehensive 360° Template");
        template.Preguntas.Should().HaveCount(12);
        template.Preguntas.Select(q => q.Orden).Should().Equal(Enumerable.Range(1, 12));
    }

    // ----- BuildAutoEvaluacionTemplate -----

    [Fact]
    public void BuildAutoEvaluacionTemplate_ReturnsTemplateScopedToCompanyWithExpectedQuestions()
    {
        var template = DefaultTemplatesSeeder.BuildAutoEvaluacionTemplate(companyId: 42);

        template.EmpresaID.Should().Be(42);
        template.Titulo.Should().Be("Self-Assessment Template");
        template.Preguntas.Should().HaveCount(11);
        template.Preguntas.Select(q => q.Orden).Should().Equal(Enumerable.Range(1, 11));
    }

    public static TheoryData<string> TemplateBuilders => new() { "180", "360", "auto" };

    private static Template BuildTemplate(string kind) => kind switch
    {
        "180" => DefaultTemplatesSeeder.BuildEvaluacion180Template(companyId: 42),
        "360" => DefaultTemplatesSeeder.BuildEvaluacion360Template(companyId: 42),
        _ => DefaultTemplatesSeeder.BuildAutoEvaluacionTemplate(companyId: 42)
    };

    [Theory]
    [MemberData(nameof(TemplateBuilders))]
    public void DefaultTemplates_GroupQuestionsInSeveralExplicitTopics(string kind)
    {
        var template = BuildTemplate(kind);

        template.Preguntas.Should().AllSatisfy(q => q.Topic.Should().NotBeNullOrWhiteSpace().And.NotBe("General"));
        template.Preguntas.Select(q => q.Topic).Distinct().Should().HaveCountGreaterThanOrEqualTo(6);
    }

    [Theory]
    [MemberData(nameof(TemplateBuilders))]
    public void DefaultTemplates_SelectionQuestionsHaveOptionsAndOthersDoNot(string kind)
    {
        var template = BuildTemplate(kind);

        template.Preguntas.Where(q => q.Tipo == QuestionType.Seleccion)
            .Should().AllSatisfy(q => q.Opciones.Should().NotBeNullOrEmpty());
        template.Preguntas.Where(q => q.Tipo != QuestionType.Seleccion)
            .Should().AllSatisfy(q => q.Opciones.Should().BeNull());
    }
}
