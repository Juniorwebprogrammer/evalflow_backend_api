using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Infrastructure.Database.Seeders;

public static class DefaultTemplatesSeeder
{
    public static async Task SeedForCompanyAsync(AppDbContext dbContext, int companyId, CancellationToken cancellationToken)
    {
        var hasTemplates = await dbContext.Templates.AnyAsync(t => t.EmpresaID == companyId, cancellationToken);
        if (hasTemplates) return;

        var templates = new List<Template>
        {
            BuildEvaluacion180Template(companyId),
            BuildEvaluacion360Template(companyId),
            BuildAutoEvaluacionTemplate(companyId)
        };

        dbContext.Templates.AddRange(templates);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static Template BuildEvaluacion180Template(int companyId) => new()
    {
        EmpresaID = companyId,
        IsDefault = true,
        Titulo = "Standard 180° Template (Manager Only)",
        Descripcion = "One-way evaluation in which the manager assesses the employee's operational performance, quality of work and key competencies.",
        FechaInicio = DateTime.UtcNow,
        FechaFin = DateTime.UtcNow.AddDays(1),
        Preguntas = new List<Question>
        {
            new() { Topic = "Results and Goals", Texto = "To what extent has the employee met the goals agreed for this period?", Tipo = QuestionType.Escala1a5, Orden = 1 },
            new() { Topic = "Results and Goals", Texto = "Do they deliver their tasks within the established deadlines?", Tipo = QuestionType.Estrellas, Orden = 2 },
            new() { Topic = "Quality of Work", Texto = "How would you rate the quality and accuracy of their work?", Tipo = QuestionType.Estrellas, Orden = 3 },
            new() { Topic = "Quality of Work", Texto = "How often does their work require corrections or additional reviews?", Tipo = QuestionType.Seleccion, Orden = 4, Opciones = ["Almost never", "Occasionally", "Frequently", "Almost always"] },
            new() { Topic = "Job Knowledge", Texto = "Do they master the tools, processes and technical knowledge their role requires?", Tipo = QuestionType.Escala1a5, Orden = 5 },
            new() { Topic = "Communication", Texto = "Do they clearly communicate the status of their work, progress and blockers?", Tipo = QuestionType.Estrellas, Orden = 6 },
            new() { Topic = "Teamwork", Texto = "Do they actively collaborate with their teammates and share useful information?", Tipo = QuestionType.Estrellas, Orden = 7 },
            new() { Topic = "Initiative and Proactivity", Texto = "Do they propose improvements or anticipate problems without being asked?", Tipo = QuestionType.Escala1a5, Orden = 8 },
            new() { Topic = "Adaptability", Texto = "How do they handle changes in priorities or context?", Tipo = QuestionType.Escala1a5, Orden = 9 },
            new() { Topic = "Commitment and Accountability", Texto = "Are they punctual and do they keep to schedules and commitments?", Tipo = QuestionType.Seleccion, Orden = 10, Opciones = ["Yes", "No"] },
            new() { Topic = "Commitment and Accountability", Texto = "Do they take responsibility for their mistakes and work to correct them?", Tipo = QuestionType.Estrellas, Orden = 11 }
        }
    };

    public static Template BuildEvaluacion360Template(int companyId) => new()
    {
        EmpresaID = companyId,
        IsDefault = true,
        Titulo = "Comprehensive 360° Template",
        Descripcion = "Complete evaluation in which the employee and their manager answer the same questions to compare self-perception with the manager's view.",
        FechaInicio = DateTime.UtcNow,
        FechaFin = DateTime.UtcNow.AddDays(1),
        Preguntas = new List<Question>
        {
            new() { Topic = "Results and Goals", Texto = "Degree of achievement of the goals agreed for the period", Tipo = QuestionType.Escala1a5, Orden = 1 },
            new() { Topic = "Results and Goals", Texto = "Meeting delivery deadlines", Tipo = QuestionType.Estrellas, Orden = 2 },
            new() { Topic = "Quality of Work", Texto = "Quality, accuracy and attention to detail of the work delivered", Tipo = QuestionType.Estrellas, Orden = 3 },
            new() { Topic = "Communication", Texto = "Clarity when communicating ideas, progress and problems to the team", Tipo = QuestionType.Estrellas, Orden = 4 },
            new() { Topic = "Communication", Texto = "Ability to listen to and take other points of view into account", Tipo = QuestionType.Escala1a5, Orden = 5 },
            new() { Topic = "Teamwork", Texto = "Collaboration with teammates and willingness to help", Tipo = QuestionType.Estrellas, Orden = 6 },
            new() { Topic = "Initiative and Proactivity", Texto = "Level of proactivity and improvement proposals in the last quarter", Tipo = QuestionType.Escala1a5, Orden = 7 },
            new() { Topic = "Problem Solving", Texto = "Ability to analyze problems and find solutions independently", Tipo = QuestionType.Escala1a5, Orden = 8 },
            new() { Topic = "Adaptability", Texto = "Flexibility when priorities, processes or tools change", Tipo = QuestionType.Estrellas, Orden = 9 },
            new() { Topic = "Leadership and Influence", Texto = "Ability to guide, motivate or be a role model for other team members", Tipo = QuestionType.Escala1a5, Orden = 10 },
            new() { Topic = "Commitment and Accountability", Texto = "Punctuality and fulfillment of commitments", Tipo = QuestionType.Seleccion, Orden = 11, Opciones = ["Yes", "No"] },
            new() { Topic = "Strengths", Texto = "Main strengths shown during the period", Tipo = QuestionType.Seleccion, Orden = 12, Opciones = ["Results orientation", "Technical knowledge", "Communication", "Teamwork", "Organization", "Creativity"] }
        }
    };

    public static Template BuildAutoEvaluacionTemplate(int companyId) => new()
    {
        EmpresaID = companyId,
        IsDefault = true,
        Titulo = "Self-Assessment Template",
        Descripcion = "Self-perception evaluation in which employees reflect on their own performance, strengths and motivation.",
        FechaInicio = DateTime.UtcNow,
        FechaFin = DateTime.UtcNow.AddDays(1),
        Preguntas = new List<Question>
        {
            new() { Topic = "Results and Goals", Texto = "To what extent do you feel you have met your goals this period?", Tipo = QuestionType.Escala1a5, Orden = 1 },
            new() { Topic = "Results and Goals", Texto = "How would you rate your ability to deliver your work on time?", Tipo = QuestionType.Estrellas, Orden = 2 },
            new() { Topic = "Quality of Work", Texto = "How satisfied are you with the quality of the work you have delivered?", Tipo = QuestionType.Estrellas, Orden = 3 },
            new() { Topic = "Job Knowledge", Texto = "How would you rate your command of the tools and knowledge your role requires?", Tipo = QuestionType.Escala1a5, Orden = 4 },
            new() { Topic = "Communication", Texto = "Do you clearly communicate your progress and blockers to your team and your manager?", Tipo = QuestionType.Estrellas, Orden = 5 },
            new() { Topic = "Teamwork", Texto = "How would you rate your collaboration with the rest of the team?", Tipo = QuestionType.Estrellas, Orden = 6 },
            new() { Topic = "Initiative and Proactivity", Texto = "How often have you proposed improvements or new ideas?", Tipo = QuestionType.Seleccion, Orden = 7, Opciones = ["Never", "Occasionally", "Frequently", "Whenever I had the chance"] },
            new() { Topic = "Commitment and Accountability", Texto = "Have you been punctual and kept your commitments?", Tipo = QuestionType.Seleccion, Orden = 8, Opciones = ["Yes", "No"] },
            new() { Topic = "Well-being and Motivation", Texto = "How would you describe your current level of motivation in your role?", Tipo = QuestionType.Escala1a5, Orden = 9 },
            new() { Topic = "Well-being and Motivation", Texto = "How do you perceive your workload during this period?", Tipo = QuestionType.Seleccion, Orden = 10, Opciones = ["Low", "Adequate", "High", "Excessive"] },
            new() { Topic = "Strengths", Texto = "Which achievements or strengths would you highlight in your own performance?", Tipo = QuestionType.Seleccion, Orden = 11, Opciones = ["Results orientation", "Technical knowledge", "Communication", "Teamwork", "Organization", "Creativity"] }
        }
    };
}
