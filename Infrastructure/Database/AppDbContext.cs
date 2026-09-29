using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Infrastructure.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleFeature> RoleFeatures => Set<RoleFeature>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateFavoriteList> TemplateFavoriteLists => Set<TemplateFavoriteList>();
    public DbSet<EvaluationCycle> EvaluationCycles => Set<EvaluationCycle>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<EvaluationSubmission> EvaluationSubmissions => Set<EvaluationSubmission>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<ClarificationRequest> ClarificationRequests => Set<ClarificationRequest>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<DiscrepancyAcceptance> DiscrepancyAcceptances => Set<DiscrepancyAcceptance>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();
    public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<AiEvaluationAnalysis> AiEvaluationAnalyses => Set<AiEvaluationAnalysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(c => c.IdentificationId).IsUnique();
            
            entity.HasMany(c => c.Usuarios)
                .WithOne(u => u.Empresa)
                .HasForeignKey(u => u.EmpresaID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Plan)
                .WithMany()
                .HasForeignKey(c => c.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.HasIndex(p => p.Code).IsUnique();
            entity.HasData(PlanCatalog.All.Select(p => new Plan
            {
                Id = p.Id,
                Code = p.Code,
                Nombre = p.Nombre,
                MaxEmployees = p.MaxEmployees,
                MaxActiveCycles = p.MaxActiveCycles,
                MaxCyclesPerYear = p.MaxCyclesPerYear,
                MaxCustomTemplates = p.MaxCustomTemplates,
                MaxDepartments = p.MaxDepartments,
                HasAiFeatures = p.HasAiFeatures,
                MaxAiAnalysesPerMonth = p.MaxAiAnalysesPerMonth,
            }));
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(c => c.Email).IsUnique();
            
            entity.HasOne(u => u.Superior)
                .WithMany(u => u.Subordinados)
                .HasForeignKey(u => u.SuperiorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(u => u.Cargo)
                .WithMany(p => p.Usuarios)
                .HasForeignKey(u => u.CargoId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            
        });

        modelBuilder.Entity<UserAvatar>(entity =>
        {
            entity.HasKey(a => a.UserId);
            entity.HasOne(a => a.User)
                .WithOne()
                .HasForeignKey<UserAvatar>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasOne(template => template.Empresa)
                .WithMany()
                .HasForeignKey(template => template.EmpresaID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(template => template.UsuariosAsignados)
                .WithMany(user => user.TemplatesAsignados);
        });

        modelBuilder.Entity<TemplateFavoriteList>(entity =>
        {
            entity.HasOne(favoriteList => favoriteList.Usuario)
                .WithMany(user => user.ListasFavoritas)
                .HasForeignKey(favoriteList => favoriteList.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(favoriteList => favoriteList.Templates)
                .WithMany(template => template.ListasFavoritas);
        });

        modelBuilder.Entity<EvaluationCycle>(entity =>
        {
            entity.HasOne(cycle => cycle.Empresa)
                .WithMany()
                .HasForeignKey(cycle => cycle.EmpresaID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(cycle => cycle.Templates)
                .WithMany(template => template.Ciclos);
        });
        
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasOne(q => q.Template)
                .WithMany(t => t.Preguntas)
                .HasForeignKey(q => q.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EvaluationSubmission>(entity =>
        {
            entity.HasOne(e => e.EvaluatedUser)
                .WithMany(u => u.EvaluacionesRecibidas)
                .HasForeignKey(e => e.EvaluatedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasOne(a => a.Submission)
                .WithMany(s => s.Answers)
                .HasForeignKey(a => a.EvaluationSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClarificationRequest>(entity =>
        {
            entity.HasOne(c => c.Cycle)
                .WithMany()
                .HasForeignKey(c => c.EvaluationCycleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Template)
                .WithMany()
                .HasForeignKey(c => c.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Question)
                .WithMany()
                .HasForeignKey(c => c.QuestionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.EvaluatedUser)
                .WithMany()
                .HasForeignKey(c => c.EvaluatedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(c => c.ManagerUser)
                .WithMany()
                .HasForeignKey(c => c.ManagerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(c => c.RequestedByUser)
                .WithMany()
                .HasForeignKey(c => c.RequestedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(c => new { c.EvaluationCycleId, c.EvaluatedUserId });
        });

        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.HasIndex(m => new { m.Status, m.NextAttemptAt });
        });

        modelBuilder.Entity<DiscrepancyAcceptance>(entity =>
        {
            entity.HasOne(a => a.Cycle)
                .WithMany()
                .HasForeignKey(a => a.EvaluationCycleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Template)
                .WithMany()
                .HasForeignKey(a => a.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.EvaluatedUser)
                .WithMany()
                .HasForeignKey(a => a.EvaluatedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.AcceptedByUser)
                .WithMany()
                .HasForeignKey(a => a.AcceptedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(a => new { a.EvaluationCycleId, a.TemplateId, a.EvaluatedUserId, a.QuestionId }).IsUnique();
        });

        modelBuilder.Entity<EvaluationResult>(entity =>
        {
            entity.HasOne(r => r.Cycle)
                .WithMany()
                .HasForeignKey(r => r.EvaluationCycleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Template)
                .WithMany()
                .HasForeignKey(r => r.TemplateId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.EvaluatedUser)
                .WithMany()
                .HasForeignKey(r => r.EvaluatedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.CompletedByUser)
                .WithMany()
                .HasForeignKey(r => r.CompletedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(r => new { r.EvaluationCycleId, r.TemplateId, r.EvaluatedUserId }).IsUnique();
            entity.HasIndex(r => r.EvaluatedUserId);
        });

        modelBuilder.Entity<AiEvaluationAnalysis>(entity =>
        {
            entity.HasOne(a => a.Empresa)
                .WithMany()
                .HasForeignKey(a => a.EmpresaID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Cycle)
                .WithMany()
                .HasForeignKey(a => a.EvaluationCycleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Template)
                .WithMany()
                .HasForeignKey(a => a.TemplateId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.EvaluatedUser)
                .WithMany()
                .HasForeignKey(a => a.EvaluatedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.RequestedByUser)
                .WithMany()
                .HasForeignKey(a => a.RequestedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(a => new { a.EvaluationCycleId, a.TemplateId, a.EvaluatedUserId });
            entity.HasIndex(a => new { a.Estado, a.NextAttemptAt });
            entity.HasIndex(a => new { a.EmpresaID, a.FechaCreacion });
        });
    }
}