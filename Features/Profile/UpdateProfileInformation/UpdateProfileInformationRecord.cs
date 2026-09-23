using MediatR;

namespace evalflow_backend_api.Features.Profile.UpdateProfileInformation;

public record UpdateProfileInformationRecord(
    string Nombre,
    string Apellidos
) : IRequest<IResult>;