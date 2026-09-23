using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace evalflow_backend_api.Infrastructure.BackgroundJobs;

public class EvaluationReminderJob(IServiceProvider serviceProvider, ILogger<EvaluationReminderJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Ejecutando proceso de recordatorios de evaluación...");
            
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al procesar los recordatorios.");
            }

            // Pausa la ejecución durante 24 horas hasta la siguiente comprobación
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken stoppingToken)
    {
        // Al ser un Singleton (BackgroundService), necesitamos crear un Scope temporal para usar AppDbContext y IEmailService
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var targetDate = DateTime.UtcNow.AddDays(3);

        // Buscamos formularios pendientes, sin avisar, en ciclos activos que caducan en <= 3 días
        var pendingSubmissions = await dbContext.EvaluationSubmissions
            .Include(s => s.RespondentUser)
            .Include(s => s.Cycle)
            .Where(s => !s.IsCompleted && !s.ReminderSent && s.Cycle!.Activo && s.Cycle.FechaFin <= targetDate)
            .ToListAsync(stoppingToken);

        if (pendingSubmissions.Count == 0) return;

        // Agrupamos por usuario para mandar un solo email
        var groupedSubmissions = pendingSubmissions.GroupBy(s => s.RespondentUser);

        foreach (var group in groupedSubmissions)
        {
            var user = group.Key!;
            var cycleName = group.First().Cycle!.Nombre;
            var daysLeft = (group.First().Cycle!.FechaFin - DateTime.UtcNow).Days;
            
            // Si es 0 o negativo, ponemos "hoy" o "vencido"
            var daysText = daysLeft > 0 ? $"en {daysLeft} días" : "muy pronto"; 

            var subject = "Recordatorio: Evaluaciones a punto de finalizar";
            var body = $"<p>Hola {user.Nombre},</p><p>Tienes <b>{group.Count()}</b> formularios pendientes en el ciclo <b>{cycleName}</b> que finalizará {daysText}. ¡No olvides completarlos!</p>";

            await emailService.SendEmailAsync(user.Email, subject, body, stoppingToken);

            // Marcamos como notificado
            foreach (var submission in group)
            {
                submission.ReminderSent = true;
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        logger.LogInformation($"Recordatorios enviados a {groupedSubmissions.Count()} usuarios.");
    }
}