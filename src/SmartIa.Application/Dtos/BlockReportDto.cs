using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;

public record BlockReportDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("typeGraphic")] string TypeGraphic,
    [property: JsonPropertyName("dimension")] string Dimension,
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("labels")] List<string> Labels,
    [property: JsonPropertyName("datasets")] List<DatasetDto> Datasets,
    [property: JsonPropertyName("kpis")] BlockKpisDto Kpis,
    [property: JsonPropertyName("insightsIa")] List<InsightItemDto> InsightsIa
);

public record BlockKpisDto(
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("media")] decimal Media,
    [property: JsonPropertyName("max")] decimal Max,
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("shareTop1")] double ShareTop1,
    [property: JsonPropertyName("shareTop3")] double ShareTop3,
    [property: JsonPropertyName("leaderLabel")] string? LeaderLabel,
    [property: JsonPropertyName("leaderValue")] decimal LeaderValue,
    [property: JsonPropertyName("topItems")] List<TopItemDto> TopItems,
    [property: JsonPropertyName("groupsShown")] int GroupsShown,
    [property: JsonPropertyName("totalGroups")] int TotalGroups,
    [property: JsonPropertyName("isTruncated")] bool IsTruncated
);

public record TopItemDto(
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("share")] double Share
);
