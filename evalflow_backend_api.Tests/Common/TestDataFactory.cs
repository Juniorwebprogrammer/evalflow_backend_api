using evalflow_backend_api.Domain.Constants;
using evalflow_backend_api.Domain.Entities;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Builders for the entities almost every feature needs (a tenant Company and a User in it),
/// filled with sensible defaults so tests only override what they actually care about.
/// </summary>
public static class TestDataFactory
{
    public static Company CreateCompany(
        string? identificationId = null,
        string nombre = "Acme Corp",
        int planId = 1,
        string planStatus = PlanStatuses.Active,
        string? billingVexxenUserId = null) => new()
    {
        Nombre = nombre,
        Colors = "#000000",
        IdentificationId = identificationId ?? $"tenant-{Guid.NewGuid():N}",
        PlanId = planId,
        PlanStatus = planStatus,
        BillingVexxenUserId = billingVexxenUserId,
        Cif = "B12345678",
    };

    public static User CreateUser(
        Company company,
        string? email = null,
        string rol = AppRoles.Employee,
        bool activo = true,
        string? vexxenUserId = null) => new()
    {
        Nombre = "Test",
        Apellidos = "User",
        Email = email ?? $"user-{Guid.NewGuid():N}@example.com",
        VexxenUserId = vexxenUserId ?? $"vx-{Guid.NewGuid():N}",
        EmpresaID = company.Id,
        Empresa = company,
        Rol = rol,
        Activo = activo,
    };
}
