using evalflow_backend_api.Domain.Enums;

namespace evalflow_backend_api.Features.Questions.QuestionDto;

public record QuestionDto(
    int Id, 
    string Texto, 
    QuestionType Tipo, 
    string Topic,
    List<string>? Opciones, 
    int Orden
);