using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;

public record DatasetDto(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("data")] List<decimal> Data
);
