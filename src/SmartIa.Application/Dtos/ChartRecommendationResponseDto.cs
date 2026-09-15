using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;

public record ChartRecommendationResponseDto
(
    [property: JsonPropertyName("recommendations")]
    List<ChartRecommendationDto> Recommendations
);