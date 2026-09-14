using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;

public record ParametersFilterDto(
    [property: JsonPropertyName("dateIni")] DateTime? DateIni,
    [property: JsonPropertyName("dateEnd")] DateTime? DateEnd,
    [property: JsonPropertyName("columnY")] string? ColumnY,
    [property: JsonPropertyName("columnX")] string? ColumnX
);
