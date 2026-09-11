using System.Text.Json.Serialization;

namespace smart_ia.Dtos;

public record ParametersFilterDto(
    [property: JsonPropertyName("dateIni")] DateTime? DateIni,
    [property: JsonPropertyName("dateEnd")] DateTime? DateEnd,
    [property: JsonPropertyName("columnY")] string? ColumnY,
    [property: JsonPropertyName("columnX")] string? ColumnX
);

public record DatasetDto(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("data")] List<decimal> Data
);

public record BlockReportDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("typeGraphic")] string TypeGraphic,
    [property: JsonPropertyName("labels")] List<string> Labels,
    [property: JsonPropertyName("datasets")] List<DatasetDto> Datasets,
    [property: JsonPropertyName("insightsIa")] string? InsightsIa
);

public record ReportResponseDto(
    [property: JsonPropertyName("blocks")] List<BlockReportDto> Blocks
);

public class ProcessReportRequest
{
    public IFormFile File { get; set; } = null!;
    public string? ParametersJson { get; set; }
}

public class DatasetMetadataDto
{
    public int TotalRows { get; set; }
    public List<ColumnSummaryDto> Columns { get; set; } = new();
}

public class ColumnSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int UniqueValuesCount { get; set; }
    public List<string> SampleValues { get; set; } = new();

}

public class ChartRecommendationDto
{
    public string ChartType { get; set; } = string.Empty;
    public string ColumnX { get; set; } = string.Empty;
    public string ColumnY { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}


public class ChartRecommendationRequest
{
    public IFormFile File { get; set; } = null!;
}