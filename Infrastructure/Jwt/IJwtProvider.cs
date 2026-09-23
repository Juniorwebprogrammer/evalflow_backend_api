using evalflow_backend_api.Domain.Entities;

namespace evalflow_backend_api.Infrastructure.Jwt;

public interface IJwtProvider
{
    string GenerateJwt(User user, Company company);
    string GenerateRefreshToken();
}