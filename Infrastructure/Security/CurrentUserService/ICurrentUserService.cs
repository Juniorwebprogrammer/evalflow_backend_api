namespace evalflow_backend_api.Infrastructure.Security.CurrentUserService;

public interface ICurrentUserService
{
    string? GetEmail();
    string? GetUserId();
    string? GetIdentificationId();
    string? GetRol();
}