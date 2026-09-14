using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;

public record BlockReportDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("typeGraphic")] string TypeGraphic,
    [property: JsonPropertyName("labels")] List<string> Labels,
    [property: JsonPropertyName("datasets")] List<DatasetDto> Datasets,
    [property: JsonPropertyName("insightsIa")] string? InsightsIa
);
