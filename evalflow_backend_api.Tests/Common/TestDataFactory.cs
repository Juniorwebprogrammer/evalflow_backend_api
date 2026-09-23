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
        int planId = 1) => new()
    {
        Nombre = nombre,
        Colors = "#000000",
        IdentificationId = identificationId ?? $"tenant-{Guid.NewGuid():N}",
        PlanId = planId,
        Cif = "B12345678",
    };

    public static User CreateUser(
        Company company,
        string? email = null,
        string passwordHash = "hashed-password",
        string rol = AppRoles.Employee,
        bool emailVerificado = true,
        bool activo = true,
        bool twoFactorEnabled = false) => new()
    {
        Nombre = "Test",
        Apellidos = "User",
        Email = email ?? $"user-{Guid.NewGuid():N}@example.com",
        PasswordHash = passwordHash,
        EmpresaID = company.Id,
        Empresa = company,
        Rol = rol,
        EmailVerificado = emailVerificado,
        Activo = activo,
        TwoFactorEnabled = twoFactorEnabled,
    };
}
