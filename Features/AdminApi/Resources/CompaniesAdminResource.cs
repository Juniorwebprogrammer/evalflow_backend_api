using System.Linq.Expressions;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Features.Plans;
using evalflow_backend_api.Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.AdminApi.Resources;

public sealed record CompanyAdminItem(
    string Id,
    string Name,
    string IdentificationId,
    string Plan,
    string PlanStatus,
    bool ManualPlan,
    int Users,
    string CreatedAt,
    string? UpdatedAt,
    string? PlanAccessEndsAt,
    string? TrialEndsAt,
    string? PlanSyncedAt,
    string? BillingVexxenUserId,
    string Cif,
    string? FiscalAddress,
    string Sector);

/// <summary>
/// The client companies, with their plan (from Vexxen, or set by hand) and how many users they have. The internal
/// company of the administrators (evalflow-internal) is left out. Action set-plan: the same as
/// PUT /Company/{identificationId}/plan (<see cref="ChangeCompanyPlanHandler"/>). Edit: name, CIF, fiscal address and
/// sector.
/// </summary>
public class CompaniesAdminResource(AppDbContext dbContext, ISender sender) : IAdminResource
{
    public const string Key = "companies";
    public const string SetPlanAction = "set-plan";
    /// <summary>The set-plan option that hands the company back to its Vexxen subscription (PlanId null).</summary>
    public const string ReturnToVexxen = "vexxen_sync";

    public static readonly IReadOnlyList<EnumOption> PlanOptions =
        PlanCatalog.All.Select(p => new EnumOption(p.Code, p.Nombre, p.Id == PlanCatalog.Enterprise ? "info" : "neutral")).ToList();

    public static readonly IReadOnlyList<EnumOption> PlanStatusOptions =
    [
        new(PlanStatuses.Trialing, "En prueba", "info"),
        new(PlanStatuses.Active, "Activo", "success"),
        new(PlanStatuses.Canceling, "Cancelándose", "warning"),
        new(PlanStatuses.PastDue, "Cobro pendiente", "warning"),
        new(PlanStatuses.None, "Sin plan (solo lectura)", "danger"),
    ];

    public static readonly ResourceDescriptor Definition = new(
        Key,
        "Empresas",
        "Empresa",
        "Clientes",
        "Las empresas que usan EvalFlow, con su plan (de Vexxen o asignado a mano). No incluye la interna de los administradores.",
        [
            new("name", "Nombre", FieldTypes.String),
            new("identificationId", "Identificador", FieldTypes.String),
            new("plan", "Plan", FieldTypes.Enum) { Enum = PlanOptions },
            new("planStatus", "Estado del plan", FieldTypes.Enum) { Enum = PlanStatusOptions },
            new("manualPlan", "Plan manual", FieldTypes.Boolean),
            new("users", "Usuarios", FieldTypes.Number),
            new("createdAt", "Alta", FieldTypes.DateTime),
            new("updatedAt", "Actualizada", FieldTypes.DateTime) { Nullable = true },
            new("planAccessEndsAt", "Acceso hasta", FieldTypes.DateTime) { Nullable = true },
            new("trialEndsAt", "Fin de la prueba", FieldTypes.DateTime) { Nullable = true },
            new("planSyncedAt", "Sincronizado con Vexxen", FieldTypes.DateTime) { Nullable = true },
            new("billingVexxenUserId", "Usuario de facturación (Vexxen)", FieldTypes.String) { Nullable = true },
            new("cif", "CIF", FieldTypes.String),
            new("fiscalAddress", "Dirección fiscal", FieldTypes.String) { Nullable = true },
            new("sector", "Sector", FieldTypes.String),
        ],
        new ListDescriptor(
            ["name", "identificationId", "plan", "planStatus", "manualPlan", "users", "createdAt"],
            "-createdAt",
            ["createdAt", "name", "users"],
            new SearchDescriptor("Nombre, identificador o CIF"),
            [
                new("planStatus", "Estado del plan", FilterTypes.Enum) { Multiple = true },
                new("plan", "Plan", FilterTypes.Enum) { Multiple = true },
                new("manualPlan", "Plan manual", FilterTypes.Boolean),
                new("createdAt", "Alta", FilterTypes.DateRange),
            ]),
        true,
        [
            new RelationDescriptor("Usuarios", UsersAdminResource.Key, "company"),
            new RelationDescriptor("Ciclos de evaluación", EvaluationCyclesAdminResource.Key, "company"),
        ],
        [
            new ActionDescriptor(
                SetPlanAction,
                "Cambiar plan",
                "Asigna un plan a mano (p. ej. Enterprise, vendido por el equipo comercial) o devuelve la empresa a la suscripción de Vexxen de su usuario de facturación.",
                "Un plan asignado a mano queda activo y la sincronización con Vexxen deja de tocarlo hasta que la devuelvas a la suscripción.",
                "default",
                [
                    new ActionInputDescriptor("planId", "Plan", FieldTypes.Enum, true)
                    {
                        Enum = [.. PlanOptions.Select(o => o with { Tone = null }), new EnumOption(ReturnToVexxen, "Volver a la suscripción de Vexxen")],
                    },
                ]),
        ])
    {
        // Only what describes the company, as its Owner/HR edit it in the app (PUT /Company/update). Not the plan
        // (set-plan), the identificationId (the tenant key in every JWT and URL), the billing user (Vexxen's
        // subscription decides it) nor the branding (logo, colours), which isn't in the record.
        Edit = new EditDescriptor(null,
        [
            new ActionInputDescriptor("name", "Nombre", FieldTypes.String, true) { MaxLength = 40 },
            new ActionInputDescriptor("cif", "CIF", FieldTypes.String, true) { MaxLength = 15 },
            new ActionInputDescriptor("fiscalAddress", "Dirección fiscal", FieldTypes.String, false) { MaxLength = 150 },
            new ActionInputDescriptor("sector", "Sector", FieldTypes.String, false) { MaxLength = 50 },
        ]),
    };

    public ResourceDescriptor Descriptor => Definition;

    /// <summary>The client companies: every company but the internal one of the administrators.</summary>
    public static IQueryable<Company> Visible(AppDbContext db) =>
        db.Companies.AsNoTracking().Where(c => c.IdentificationId != CreateAdministratorCommand.InternalCompanyId);

    public static string PlanCode(int planId) => PlanCatalog.All.FirstOrDefault(p => p.Id == planId)?.Code ?? $"plan_{planId}";

    private static readonly Expression<Func<Company, CompanyAdminItem>> ToItem = c => new CompanyAdminItem(
        c.Id.ToString(),
        c.Nombre,
        c.IdentificationId,
        PlanCode(c.PlanId),
        c.PlanStatus,
        c.PlanManual,
        c.Usuarios.Count,
        AdminFormat.DateTime(c.FechaCreacion),
        AdminFormat.DateTime(c.FechaActualizacion),
        AdminFormat.DateTime(c.PlanAccessEndsAt),
        AdminFormat.DateTime(c.TrialEndsAt),
        AdminFormat.DateTime(c.PlanSyncedAt),
        c.BillingVexxenUserId,
        c.Cif,
        c.DireccionFiscal,
        c.Sector);

    public async Task<AdminListResult> ListAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var companies = Visible(dbContext);

        if (query.Q is { } q)
        {
            var term = q.ToLowerInvariant();
            companies = companies.Where(c => c.Nombre.ToLower().Contains(term) || c.IdentificationId.ToLower().Contains(term)
                                             || c.Cif.ToLower().Contains(term));
        }
        if (query.Filter<EnumFilterValue>("planStatus") is { } planStatus)
        {
            var statuses = planStatus.Values.ToList();
            companies = companies.Where(c => statuses.Contains(c.PlanStatus));
        }
        if (query.Filter<EnumFilterValue>("plan") is { } plan)
        {
            var planIds = PlanCatalog.All.Where(p => plan.Values.Contains(p.Code)).Select(p => p.Id).ToList();
            companies = companies.Where(c => planIds.Contains(c.PlanId));
        }
        if (query.Filter<BooleanFilterValue>("manualPlan") is { } manual)
        {
            companies = companies.Where(c => c.PlanManual == manual.Value);
        }
        if (query.Filter<DateRangeFilterValue>("createdAt") is { } createdAt)
        {
            if (createdAt.From is { } from) companies = companies.Where(c => c.FechaCreacion >= from);
            if (createdAt.To is { } to) companies = companies.Where(c => c.FechaCreacion <= to);
        }

        var total = await companies.CountAsync(cancellationToken);
        var sorted = (query.Sort.Key, query.Sort.Descending) switch
        {
            ("name", false) => companies.OrderBy(c => c.Nombre).ThenBy(c => c.Id),
            ("name", true) => companies.OrderByDescending(c => c.Nombre).ThenByDescending(c => c.Id),
            ("users", false) => companies.OrderBy(c => c.Usuarios.Count).ThenBy(c => c.Id),
            ("users", true) => companies.OrderByDescending(c => c.Usuarios.Count).ThenByDescending(c => c.Id),
            (_, false) => companies.OrderBy(c => c.FechaCreacion).ThenBy(c => c.Id),
            (_, true) => companies.OrderByDescending(c => c.FechaCreacion).ThenByDescending(c => c.Id),
        };
        var items = await sorted.Page(query).Select(ToItem).ToListAsync(cancellationToken);
        return new AdminListResult(items, total);
    }

    public async Task<object?> GetAsync(string id, CancellationToken cancellationToken) => await FindAsync(id, cancellationToken);

    private async Task<CompanyAdminItem?> FindAsync(string id, CancellationToken cancellationToken)
    {
        if (AdminQueryExtensions.ParseId(id) is not { } companyId) return null;
        return await Visible(dbContext).Where(c => c.Id == companyId).Select(ToItem).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<object> RunActionAsync(string action, string id, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken)
    {
        if (action != SetPlanAction) throw AdminApiException.NotFound($"{Key} has no action {action}");

        var companyId = AdminQueryExtensions.ParseId(id) ?? throw AdminApiException.NotFound($"{Key}/{id} doesn't exist");
        var company = await Visible(dbContext)
            .Where(c => c.Id == companyId)
            .Select(c => new { c.IdentificationId, c.BillingVexxenUserId })
            .FirstOrDefaultAsync(cancellationToken) ?? throw AdminApiException.NotFound($"{Key}/{id} doesn't exist");

        var choice = (string)inputs["planId"]!;
        int? planId = null;
        if (choice != ReturnToVexxen)
        {
            planId = PlanCatalog.FromVexxenPlanId(choice)
                     ?? throw AdminApiException.InvalidInput($"Unknown plan {choice}", [new("planId", $"Invalid option: {choice}")]);
        }
        else if (string.IsNullOrWhiteSpace(company.BillingVexxenUserId))
        {
            // Nobody's subscription to read the plan from: it would keep the last plan forever, unmanaged.
            throw AdminApiException.Conflict("The company has no billing user in Vexxen: it can only have a plan set by hand.");
        }

        var result = await sender.Send(new ChangeCompanyPlanRecord(company.IdentificationId, planId), cancellationToken);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 } failed)
        {
            throw failed.StatusCode == StatusCodes.Status404NotFound
                ? AdminApiException.NotFound($"{Key}/{id} doesn't exist")
                : AdminApiException.Conflict($"The plan couldn't be changed ({failed.StatusCode})");
        }

        return await FindAsync(id, cancellationToken) ?? throw AdminApiException.NotFound($"{Key}/{id} doesn't exist");
    }

    public async Task<object> EditAsync(string id, IReadOnlyDictionary<string, object?> changes, CancellationToken cancellationToken)
    {
        var companyId = AdminQueryExtensions.ParseId(id) ?? throw AdminApiException.NotFound($"{Key}/{id} doesn't exist");
        var company = await dbContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.IdentificationId != CreateAdministratorCommand.InternalCompanyId, cancellationToken)
            ?? throw AdminApiException.NotFound($"{Key}/{id} doesn't exist");

        foreach (var (field, value) in changes)
        {
            var text = (string?)value;
            switch (field)
            {
                case "name": company.Nombre = text!; break;
                case "cif": company.Cif = text!; break;
                case "fiscalAddress": company.DireccionFiscal = text; break;
                // Not nullable in the database: empty is "".
                case "sector": company.Sector = text ?? ""; break;
                default: throw AdminApiException.InvalidInput($"{field} isn't editable", [new(field, "Not editable")]);
            }
        }
        company.FechaActualizacion = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await FindAsync(id, cancellationToken) ?? throw AdminApiException.NotFound($"{Key}/{id} doesn't exist");
    }
}
