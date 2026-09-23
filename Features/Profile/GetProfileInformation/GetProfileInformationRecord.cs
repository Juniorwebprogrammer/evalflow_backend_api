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
    bool TwoFactorAuthentication
);