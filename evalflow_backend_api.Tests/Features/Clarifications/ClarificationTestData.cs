using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.Clarifications;

public sealed record ClarificationScenario(
    Company Company, EvaluationCycle Cycle, Template Template, Question Question,
    User Rrhh, User Employee, User Manager);

public static class ClarificationTestData
{
    public static async Task<ClarificationScenario> SeedAsync(AppDbContext db, string tenant = "tenant-1",
        EvaluationType tipo = EvaluationType.Evaluacion360, bool selfCompleted = true, bool managerCompleted = true)
    {
        var company = TestDataFactory.CreateCompany(identificationId: tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
            TipoEvaluación = tipo,
        };
        var template = new Template
        {
            Titulo = "Desempeño",
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(30),
            EmpresaID = company.Id,
        };
        var rrhh = TestDataFactory.CreateUser(company, email: $"rrhh-{tenant}@example.com", rol: AppRoles.Rrhh);
        rrhh.Nombre = "Rosa";
        var manager = TestDataFactory.CreateUser(company, email: $"manager-{tenant}@example.com", rol: AppRoles.Superior);
        manager.Nombre = "Marta";
        db.EvaluationCycles.Add(cycle);
        db.Templates.Add(template);
        db.Users.AddRange(rrhh, manager);
        await db.SaveChangesAsync();

        var employee = TestDataFactory.CreateUser(company, email: $"employee-{tenant}@example.com");
        employee.Nombre = "Enrique";
        employee.SuperiorId = manager.Id;
        var question = new Question { Texto = "Cumple plazos", Tipo = QuestionType.Estrellas, Orden = 1, TemplateId = template.Id };
        db.Users.Add(employee);
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        db.EvaluationSubmissions.AddRange(
            Submission(cycle, template, employee, employee, selfCompleted),
            Submission(cycle, template, employee, manager, managerCompleted));
        await db.SaveChangesAsync();

        return new ClarificationScenario(company, cycle, template, question, rrhh, employee, manager);
    }

    public static async Task<ClarificationRequest> AddClarificationAsync(AppDbContext db, ClarificationScenario s,
        string? evaluatedResponse = null, string? managerResponse = null)
    {
        var clarification = new ClarificationRequest
        {
            EvaluationCycleId = s.Cycle.Id,
            TemplateId = s.Template.Id,
            QuestionId = s.Question.Id,
            EvaluatedUserId = s.Employee.Id,
            ManagerUserId = s.Manager.Id,
            RequestedByUserId = s.Rrhh.Id,
            Mensaje = "¿Por qué esa nota?",
            EvaluatedResponseEncrypted = evaluatedResponse,
            EvaluatedRespondedAt = evaluatedResponse is null ? null : DateTime.UtcNow,
            ManagerResponseEncrypted = managerResponse,
            ManagerRespondedAt = managerResponse is null ? null : DateTime.UtcNow,
        };
        db.ClarificationRequests.Add(clarification);
        await db.SaveChangesAsync();
        return clarification;
    }

    private static EvaluationSubmission Submission(EvaluationCycle cycle, Template template, User evaluated, User respondent, bool completed) => new()
    {
        EvaluationCycleId = cycle.Id,
        TemplateId = template.Id,
        EvaluatedUserId = evaluated.Id,
        RespondentUserId = respondent.Id,
        IsCompleted = completed,
        SubmittedAt = completed ? DateTime.UtcNow : null,
    };
}
