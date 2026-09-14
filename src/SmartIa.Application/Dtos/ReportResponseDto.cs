using System.Text.Json.Serialization;

namespace SmartIa.Application.Dtos;

public record ReportResponseDto(
    [property: JsonPropertyName("blocks")] List<BlockReportDto> Blocks
);
