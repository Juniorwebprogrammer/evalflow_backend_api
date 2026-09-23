using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Features.Auth.Login; 
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Jwt;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.Auth.Verify2FA;

public class Verify2FaHandler(AppDbContext dbContext, IJwtProvider jwtProvider) : IRequestHandler<Verify2FARecord, IResult>
{
    public async Task<IResult> Handle(Verify2FARecord request, CancellationToken cancellationToken)
    {
        var user = await GetUserWithCompanyAsync(request.Email, cancellationToken);
        if (user is null) 
        {
            return Results.BadRequest(new { Message = "Usuario no encontrado." });
        }

        var validationError = ValidateCode(user, request.Code);
        if (validationError is not null) return validationError;

        await Consume2FaCodeAndSaveAsync(user, cancellationToken);

        var (jwt, refreshToken) = GenerateTokens(user);

        return GenerateSuccessResponse(user, jwt, refreshToken);
    }

    private async Task<User?> GetUserWithCompanyAsync(string email, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    private IResult? ValidateCode(User user, string inputCode)
    {
        if (string.IsNullOrWhiteSpace(user.TwoFactorCode))
        {
            return Results.BadRequest(new { Message = "No hay ningún código pendiente de validación." });
        }

        if (user.TwoFactorCodeExpiration < DateTime.UtcNow)
        {
            return Results.BadRequest(new { Message = "El código ha caducado. Vuelve a iniciar sesión para recibir otro." });
        }

        if (!string.Equals(user.TwoFactorCode, inputCode, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { Message = "El código introducido es incorrecto." });
        }

        return null;
    }

    private async Task Consume2FaCodeAndSaveAsync(User user, CancellationToken cancellationToken)
    {
        user.TwoFactorCode = null;
        user.TwoFactorCodeExpiration = null;
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private (string Jwt, string RefreshToken) GenerateTokens(User user)
    {
        if (user.Empresa == null) return (null, null)!;
        var jwt = jwtProvider.GenerateJwt(user, user.Empresa);
        var refreshToken = jwtProvider.GenerateRefreshToken();
        
        return (jwt, refreshToken);

    }

    private static IResult GenerateSuccessResponse(User user, string jwt, string refreshToken)
    {
        var responsePayload = new LoginResponse(
            Message: "Autenticación completada con éxito.",
            Username: user.Nombre,
            Jwt: jwt,
            RefreshToken: refreshToken,
            TwoFactorEnabled: user.TwoFactorEnabled
        );

        return Results.Ok(responsePayload);
    }
}