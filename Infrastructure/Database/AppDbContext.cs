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
    }
}