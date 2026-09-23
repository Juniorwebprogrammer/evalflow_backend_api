namespace evalflow_backend_api.Features.Templates.TemplatesDto;

public record TemplateSummaryDto(
    int Id, 
    string Titulo,
    string? Descripcion,
    DateTime FechaInicio, 
    DateTime FechaFin, 
    int UsuariosCount,
    List<int> AssignedUserIds
);

public record TemplateDetailsDto(
    int Id, 
    string Titulo, 
    string? Descripcion, 
    DateTime FechaInicio, 
    DateTime FechaFin, 
    List<int> AssignedUserIds 
);