using System.Linq.Expressions;
using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Admin.CreateAdministrator;
using evalflow_backend_api.Features.AdminApi.Contract;
using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.AdminApi.Resources;

public sealed record UserAdminItem(
    string Id,
    string Email,
    string Name,
    AdminRef Company,
    string Role,
    string Status,
    string CreatedAt,
    string? UpdatedAt,
    string? Department,
    string? JobPosition,
    AdminRef? Superior,
    string? VexxenUserId,
    string? InvitationExpiresAt);

/// <summary>
/// The people of the client companies (the administrators, in the internal company, are left out). Status: active
/// (signed in with Vexxen), invited (the invitation hasn't been accepted yet) or inactive (disabled by the company).
/// The invitation token is never returned, only when it expires.
/// </summary>
public class UsersAdminResource(AppDbContext dbContext) : IAdminResource
{
    public const string Key = "users";

    /// <summary>Contract value (snake_case) → the role as EvalFlow stores it.</summary>
    private static readonly Dictionary<string, string> RoleValues = new()
    {
        ["owner"] = AppRoles.Owner,
        ["rrhh"] = AppRoles.Rrhh,
        ["superior"] = AppRoles.Superior,
        ["employee"] = AppRoles.Employee,
    };

    public static readonly IReadOnlyList<EnumOption> RoleOptions =
    [
        new("owner", "Propietario", "info"),
        new("rrhh", "RRHH", "info"),
        new("superior", "Responsable", "neutral"),
        new("employee", "Empleado", "neutral"),
    ];

    public static readonly IReadOnlyList<EnumOption> StatusOptions =
    [
        new("active", "Activo", "success"),
        new("invited", "Invitación pendiente", "warning"),
        new("inactive", "Desactivado", "neutral"),
    ];

    public static readonly ResourceDescriptor Definition = new(
        Key,
        "Usuarios",
        "Usuario",
        "Clientes",
        "Las personas de las empresas cliente. Entran con su cuenta de Vexxen.",
        [
            new("email", "Email", FieldTypes.Email),
            new("name", "Nombre", FieldTypes.String),
            new("company", "Empresa", FieldTypes.Ref) { Ref = new RefTarget(CompaniesAdminResource.Key) },
            new("role", "Rol", FieldTypes.Enum) { Enum = RoleOptions },
            new("status", "Estado", FieldTypes.Enum) { Enum = StatusOptions },
            new("createdAt", "Alta", FieldTypes.DateTime),
            new("updatedAt", "Actualizado", FieldTypes.DateTime) { Nullable = true },
            new("department", "Departamento", FieldTypes.String) { Nullable = true },
            new("jobPosition", "Puesto", FieldTypes.String) { Nullable = true },
            new("superior", "Responsable", FieldTypes.Ref) { Ref = new RefTarget(Key), Nullable = true },
            new("vexxenUserId", "Cuenta de Vexxen", FieldTypes.String) { Nullable = true },
            new("invitationExpiresAt", "Caduca la invitación", FieldTypes.DateTime) { Nullable = true },
        ],
        new ListDescriptor(
            ["email", "name", "company", "role", "status", "createdAt"],
            "-createdAt",
            ["createdAt", "email", "name"],
            new SearchDescriptor("Email o nombre"),
            [
                new("role", "Rol", FilterTypes.Enum) { Multiple = true },
                new("status", "Estado", FilterTypes.Enum) { Multiple = true },
                new("company", "Empresa", FilterTypes.Ref) { Ref = new RefTarget(CompaniesAdminResource.Key) },
                new("createdAt", "Alta", FilterTypes.DateRange),
            ]),
        true,
        [],
        []);

    public ResourceDescriptor Descriptor => Definition;

    public static IQueryable<User> Visible(AppDbContext db) =>
        db.Users.AsNoTracking().Where(u => u.Empresa!.IdentificationId != CreateAdministratorCommand.InternalCompanyId);

    public static string RoleValue(string role) =>
        RoleValues.FirstOrDefault(r => r.Value == role).Key ?? role.ToLowerInvariant();

    public static string StatusValue(bool active, string? vexxenUserId) =>
        !active ? "inactive" : vexxenUserId is null ? "invited" : "active";

    private static readonly Expression<Func<User, UserAdminItem>> ToItem = u => new UserAdminItem(
        u.Id.ToString(),
        u.Email,
        u.Nombre + " " + u.Apellidos,
        new AdminRef(u.EmpresaID.ToString(), u.Empresa!.Nombre),
        RoleValue(u.Rol),
        StatusValue(u.Activo, u.VexxenUserId),
        AdminFormat.DateTime(u.FechaCreacion),
        AdminFormat.DateTime(u.FechaActualizacion),
        u.Departamento != null ? u.Departamento.Nombre : null,
        u.Cargo != null ? u.Cargo.Nombre : null,
        u.Superior != null ? new AdminRef(u.Superior.Id.ToString(), u.Superior.Nombre + " " + u.Superior.Apellidos) : null,
        u.VexxenUserId,
        u.VexxenUserId == null ? AdminFormat.DateTime(u.TokenInvitacionExpiracion) : null);

    public async Task<AdminListResult> ListAsync(AdminListQuery query, CancellationToken cancellationToken)
    {
        var users = Visible(dbContext);

        if (query.Q is { } q)
        {
            var term = q.ToLowerInvariant();
            users = users.Where(u => u.Email.ToLower().Contains(term) || (u.Nombre + " " + u.Apellidos).ToLower().Contains(term));
        }
        if (query.Filter<EnumFilterValue>("role") is { } role)
        {
            var roles = role.Values.Select(v => RoleValues[v]).ToList();
            users = users.Where(u => roles.Contains(u.Rol));
        }
        if (query.Filter<EnumFilterValue>("status") is { } status)
        {
            var active = status.Values.Contains("active");
            var invited = status.Values.Contains("invited");
            var inactive = status.Values.Contains("inactive");
            users = users.Where(u => (active && u.Activo && u.VexxenUserId != null)
                                     || (invited && u.Activo && u.VexxenUserId == null)
                                     || (inactive && !u.Activo));
        }
        if (query.Filter<RefFilterValue>("company") is { } company)
        {
            var companyId = AdminQueryExtensions.RefId(company);
            users = users.Where(u => u.EmpresaID == companyId);
        }
        if (query.Filter<DateRangeFilterValue>("createdAt") is { } createdAt)
        {
            if (createdAt.From is { } from) users = users.Where(u => u.FechaCreacion >= from);
            if (createdAt.To is { } to) users = users.Where(u => u.FechaCreacion <= to);
        }

        var total = await users.CountAsync(cancellationToken);
        var sorted = (query.Sort.Key, query.Sort.Descending) switch
        {
            ("email", false) => users.OrderBy(u => u.Email).ThenBy(u => u.Id),
            ("email", true) => users.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id),
            ("name", false) => users.OrderBy(u => u.Nombre).ThenBy(u => u.Apellidos).ThenBy(u => u.Id),
            ("name", true) => users.OrderByDescending(u => u.Nombre).ThenByDescending(u => u.Apellidos).ThenByDescending(u => u.Id),
            (_, false) => users.OrderBy(u => u.FechaCreacion).ThenBy(u => u.Id),
            (_, true) => users.OrderByDescending(u => u.FechaCreacion).ThenByDescending(u => u.Id),
        };
        var items = await sorted.Page(query).Select(ToItem).ToListAsync(cancellationToken);
        return new AdminListResult(items, total);
    }

    public async Task<object?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (AdminQueryExtensions.ParseId(id) is not { } userId) return null;
        return await Visible(dbContext).Where(u => u.Id == userId).Select(ToItem).FirstOrDefaultAsync(cancellationToken);
    }
}
