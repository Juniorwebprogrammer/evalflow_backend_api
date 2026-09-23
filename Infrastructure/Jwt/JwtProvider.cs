using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using evalflow_backend_api.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace evalflow_backend_api.Infrastructure.Jwt;

public sealed class JwtProvider(IConfiguration configuration) : IJwtProvider
{
    public string GenerateJwt(User user, Company company)
    {
        var secretKey = configuration["Jwt:Secret"];
        if (secretKey is null) return null!;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("identificationId", company.IdentificationId),
            new Claim(ClaimTypes.Role, user.Rol),
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(configuration["Jwt:ExpirationInMinutes"])),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];

        using var range = RandomNumberGenerator.Create();
        
        range.GetBytes(randomNumber);
        
        return Convert.ToBase64String(randomNumber);
    }
}