namespace evalflow_backend_api.Features.JobPosittions.JobPositionDto;

public record JobPositionDto(
    int Id, 
    string Nombre, 
    string? Descripcion, 
    int EmpleadosCount
);