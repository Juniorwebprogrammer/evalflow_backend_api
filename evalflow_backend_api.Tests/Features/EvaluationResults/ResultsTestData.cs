using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Tests.Common;

namespace evalflow_backend_api.Tests.Features.EvaluationResults;

public sealed record ResultsScenario(
    Company Company, EvaluationCycle Cycle, Template Template, Question Aligned, Question Imbalanced, Question Selection,
    User Rrhh, User Employee, User Manager, EvaluationSubmission SelfSubmission, EvaluationSubmission ManagerSubmission);

public static class ResultsTestData
{
    public static async Task<ResultsScenario> SeedAsync(AppDbContext db, Func<string, string> encrypt,
        string tenant = "tenant-1", EvaluationType tipo = EvaluationType.Evaluacion360, bool managerCompleted = true)
    {
        var company = TestDataFactory.CreateCompany(identificationId: tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var cycle = new EvaluationCycle
        {
            Nombre = "Q1",
            Activo = true,
            FechaInicio = DateTime.UtcNow.AddDays(-10),
            FechaFin = DateTime.UtcNow.AddDays(20),
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
        var department = new Department { Nombre = "Ventas", EmpresaID = company.Id };
        var position = new JobPosition { Nombre = "Comercial", EmpresaID = company.Id };
        var rrhh = TestDataFactory.CreateUser(company, email: $"rrhh-{tenant}@example.com", rol: AppRoles.Rrhh);
        var manager = TestDataFactory.CreateUser(company, email: $"manager-{tenant}@example.com", rol: AppRoles.Superior);
        manager.Nombre = "Marta";
        db.EvaluationCycles.Add(cycle);
        db.Templates.Add(template);
        db.Departments.Add(department);
        db.JobPositions.Add(position);
        db.Users.AddRange(rrhh, manager);
        await db.SaveChangesAsync();

        var employee = TestDataFactory.CreateUser(company, email: $"employee-{tenant}@example.com");
        employee.Nombre = "Enrique";
        employee.SuperiorId = manager.Id;
        employee.DepartamentoId = department.Id;
        employee.CargoId = position.Id;
        var aligned = new Question { Texto = "Trabajo en equipo", Tipo = QuestionType.Estrellas, Orden = 1, TemplateId = template.Id };
        var imbalanced = new Question { Texto = "Cumple plazos", Tipo = QuestionType.Escala1a5, Orden = 2, TemplateId = template.Id };
        var selection = new Question { Texto = "Fortalezas", Tipo = QuestionType.Seleccion, Orden = 3, Opciones = ["A", "B"], TemplateId = template.Id };
        db.Users.Add(employee);
        db.Questions.AddRange(aligned, imbalanced, selection);
        await db.SaveChangesAsync();

        var self = Submission(cycle, template, employee, employee, true, encrypt,
            (aligned.Id, "4"), (imbalanced.Id, "5"), (selection.Id, "[\"A\"]"));
        var managerSubmission = Submission(cycle, template, employee, manager, managerCompleted, encrypt,
            (aligned.Id, "4"), (imbalanced.Id, managerCompleted ? "2" : null), (selection.Id, "[\"A\"]"));
        db.EvaluationSubmissions.AddRange(self, managerSubmission);
        await db.SaveChangesAsync();

        return new ResultsScenario(company, cycle, template, aligned, imbalanced, selection, rrhh, employee, manager, self, managerSubmission);
    }

    private static EvaluationSubmission Submission(EvaluationCycle cycle, Template template, User evaluated, User respondent, bool completed,
        Func<string, string> encrypt, params (int QuestionId, string? Raw)[] answers) => new()
    {
        EvaluationCycleId = cycle.Id,
        TemplateId = template.Id,
        EvaluatedUserId = evaluated.Id,
        RespondentUserId = respondent.Id,
        IsCompleted = completed,
        SubmittedAt = completed ? DateTime.UtcNow : null,
        Answers = answers
            .Select(a => new Answer { QuestionId = a.QuestionId, EncryptedPayload = a.Raw is null ? string.Empty : encrypt(a.Raw) })
            .ToList(),
    };
}
