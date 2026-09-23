namespace evalflow_backend_api.Infrastructure.Security.PasswordHasher;

public interface IPasswordHasser
{
    string Hash(string password);
    bool Verify(string password, string hash);
}