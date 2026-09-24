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
        Titulo = "Plantilla Estándar 180° (Solo Mánager)",
        Descripcion = "Evaluación unidireccional enfocada en el rendimiento operativo.",
        FechaInicio = DateTime.UtcNow,
        FechaFin = DateTime.UtcNow.AddDays(1),
        Preguntas = new List<Question>
        {
            new() { Texto = "¿Cumple con los objetivos establecidos?", Tipo = QuestionType.Estrellas, Orden = 1 },
            new() { Texto = "¿Es puntual en su llegada?", Tipo = QuestionType.Seleccion, Orden = 2, Opciones = ["Sí", "No"]}
        }
    };

    public static Template BuildEvaluacion360Template(int companyId) => new()
    {
        EmpresaID = companyId,
        Titulo = "Plantilla Integral 360°",
        Descripcion = "Evaluación completa incluyendo autopercepción.",
        FechaInicio = DateTime.UtcNow,
        FechaFin = DateTime.UtcNow.AddDays(1),
        Preguntas = new List<Question>
        {
            new() { Texto = "Nivel de proactividad en el último trimestre", Tipo = QuestionType.Escala1a5, Orden = 1 },
            new() { Texto = "¿Cómo calificarías la comunicación con el equipo?", Tipo = QuestionType.Estrellas, Orden = 2 },
            new() { Texto = "¿Es puntual en su llegada?", Tipo = QuestionType.Seleccion, Orden = 3, Opciones = ["Sí", "No"] }
        }
    };

    public static Template BuildAutoEvaluacionTemplate(int companyId) => new()
    {
        EmpresaID = companyId,
        Titulo = "Plantilla de Autoevaluación",
        Descripcion = "Evaluación de autopercepción para que el propio empleado valore su desempeño.",
        FechaInicio = DateTime.UtcNow,
        FechaFin = DateTime.UtcNow.AddDays(1),
        Preguntas = new List<Question>
        {
            new() { Texto = "¿Consideras que has cumplido tus objetivos en este periodo?", Tipo = QuestionType.Estrellas, Orden = 1 },
            new() { Texto = "¿Qué logros destacarías de tu propio desempeño?", Tipo = QuestionType.Seleccion, Orden = 2 },
            new() { Texto = "¿Es puntual en su llegada?", Tipo = QuestionType.Seleccion, Orden = 3, Opciones = ["Sí", "No"]}
        }
    };
}
