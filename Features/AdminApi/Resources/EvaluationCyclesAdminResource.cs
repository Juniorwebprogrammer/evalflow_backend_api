using System.Linq.Expressions;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Domain.Enums;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.AdminApi.Resources;

public sealed record EvaluationCycleAdminItem(
    string Id,
    AdminRef Company,
    string Name,
    string Status,
    string Type,
    string StartDate,
    string EndDate,
    string? CompletedAt,
    string? Description);

/// <summary>
/// The evaluation cycles of the client companies. Status: active (running), inactive (created or paused, not
/// finished) or completed. Active is what counts against the plan's active-cycles limit (PlanLimitService).
/// </summary>
public class EvaluationCyclesAdminResource(AppDbContext dbContext) : IAdminResource
{
    public const string Key = "evaluation-cycles";

    public static readonly IReadOnlyList<EnumOption> StatusOptions =
    [
        new("active", "Activo", "success"),
        new("inactive", "Inactivo", "neutral"),
        new("completed", "Completado", "info"),
    ];

    public static readonly IReadOnlyList<EnumOption> TypeOptions =
    [
        new("self", "Autoevaluación"),
        new("evaluation_180", "180°"),
        new("evaluation_360", "360°"),
    ];

    public static readonly ResourceDescriptor Definition = new(
        Key,
        "Ciclos de evaluación",
        "Ciclo de evaluación",
        "Evaluaciones",
        null,
        [
            new("company", "Empresa", FieldTypes.Ref) { Ref = new RefTarget(CompaniesAdminResource.Key) },
            new("name", "Nombre", FieldTypes.String),
            new("status", "Estado", FieldTypes.Enum) { Enum = StatusOptions },
            new("type", "Tipo", FieldTypes.Enum) { Enum = TypeOptions },
            new("startDate", "Inicio", FieldTypes.DateTime),
            new("endDate", "Fin", FieldTypes.DateTime),
            new("completedAt", "Completado", FieldTypes.DateTime) { Nullable = true },
            new("description", "Descripción", FieldTypes.Text) { Nullable = true },
        ],
        new ListDescriptor(
            ["company", "name", "status", "startDate", "endDate"],
            "-startDate",
            ["startDate", "endDate", "name"],
            new SearchDescriptor("Nombre del ciclo"),
            [
                new("company", "Empresa", FilterTypes.Ref) { Ref = new RefTarget(CompaniesAdminResource.Key) },
                new("status", "Estado", FilterTypes.Enum) { Multiple = true },
            ]),
        true,
        [],
        []);

    public ResourceDescriptor Descriptor => Definition;

    public static IQueryable<EvaluationCycle> Visible(AppDbContext db) =>
        db.EvaluationCycles.AsNoTracking().Where(c => c.Empresa!.IdentificationId != CreateAdministratorCommand.InternalCompanyId);

    public static string StatusValue(bool active, DateTime? completedAt) =>
        completedAt is not null ? "completed" : active ? "active" : "inactive";

    public static string TypeValue(EvaluationType type) => type switch
    {
        EvaluationType.Auto => "self",
        EvaluationType.Evaluacion180 => "evaluation_180",
        EvaluationType.Evaluacion360 => "evaluation_360",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static readonly Expression<Func<EvaluationCycle, EvaluationCycleAdminItem>> ToItem = c => new EvaluationCycleAdminItem(
        c.Id.ToString(),
        new AdminRef(c.EmpresaID.ToString(), c.Empresa!.Nombre),
        c.Nombre,
        StatusValue(c.Activo, c.FechaCompletado),
        TypeValue(c.TipoEvaluación),
        AdminFormat.DateTime(c.FechaInicio),
        AdminFormat.DateTime(c.FechaFin),
        AdminFormat.DateTime(c.FechaCompletado),
        c.Descripcion);

    public async Task<AdminListResult> ListAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var cycles = Visible(dbContext);

        if (query.Q is { } q)
        {
            var term = q.ToLowerInvariant();
            cycles = cycles.Where(c => c.Nombre.ToLower().Contains(term));
        }
        if (query.Filter<RefFilterValue>("company") is { } company)
        {
            var companyId = AdminQueryExtensions.RefId(company);
            cycles = cycles.Where(c => c.EmpresaID == companyId);
        }
        if (query.Filter<EnumFilterValue>("status") is { } status)
        {
            var active = status.Values.Contains("active");
            var inactive = status.Values.Contains("inactive");
            var completed = status.Values.Contains("completed");
            cycles = cycles.Where(c => (completed && c.FechaCompletado != null)
                                       || (active && c.Activo && c.FechaCompletado == null)
                                       || (inactive && !c.Activo && c.FechaCompletado == null));
        }

        var total = await cycles.CountAsync(cancellationToken);
        var sorted = (query.Sort.Key, query.Sort.Descending) switch
        {
            ("endDate", false) => cycles.OrderBy(c => c.FechaFin).ThenBy(c => c.Id),
            ("endDate", true) => cycles.OrderByDescending(c => c.FechaFin).ThenByDescending(c => c.Id),
            ("name", false) => cycles.OrderBy(c => c.Nombre).ThenBy(c => c.Id),
            ("name", true) => cycles.OrderByDescending(c => c.Nombre).ThenByDescending(c => c.Id),
            (_, false) => cycles.OrderBy(c => c.FechaInicio).ThenBy(c => c.Id),
            (_, true) => cycles.OrderByDescending(c => c.FechaInicio).ThenByDescending(c => c.Id),
        };
        var items = await sorted.Page(query).Select(ToItem).ToListAsync(cancellationToken);
        return new AdminListResult(items, total);
    }

    public async Task<object?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (AdminQueryExtensions.ParseId(id) is not { } cycleId) return null;
        return await Visible(dbContext).Where(c => c.Id == cycleId).Select(ToItem).FirstOrDefaultAsync(cancellationToken);
    }
}
