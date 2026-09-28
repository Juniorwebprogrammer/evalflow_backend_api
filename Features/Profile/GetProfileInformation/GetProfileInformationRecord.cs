using MediatR;

namespace evalflow_backend_api.Features.Profile.GetProfileInformation;

public record GetProfileInformationRecord() : IRequest<IResult>;

public record GetProfileInformationDTO(
    string Nombre,
    string Apellidos,
    string Email,
    string Rol,
    DateTime FechaCreacion,
    string NombreEmpresa,
    string IdentificationId,
    bool TwoFactorAuthentication,
    /// <summary>When the profile picture last changed; null without one. Doubles as a cache-buster.</summary>
    DateTime? AvatarUpdatedAt
);