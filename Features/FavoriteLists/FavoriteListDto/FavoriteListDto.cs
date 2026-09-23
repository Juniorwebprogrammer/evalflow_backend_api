namespace evalflow_backend_api.Features.FavoriteLists.FavoriteListDto;

public record FavoriteListDto(
    int Id,
    string Nombre,
    string? Descripcion,
    int TemplatesAccount,
    List<int> TemplateIds
);