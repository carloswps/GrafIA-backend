using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;


public record ReportResponseDto(
    [property: JsonPropertyName("blocks")] List<BlockReportDto> Blocks,
    [property: JsonPropertyName("context")] ReportContextDto Context
);

public record ReportContextDto(
    [property: JsonPropertyName("rowsProcessed")] int RowsProcessed,
    [property: JsonPropertyName("generatedAt")] DateTime GeneratedAt,
    [property: JsonPropertyName("periodStart")] DateTime? PeriodStart,
    [property: JsonPropertyName("periodEnd")] DateTime? PeriodEnd,
    [property: JsonPropertyName("appliedFilters")] ParametersFilterDto? AppliedFilters
);