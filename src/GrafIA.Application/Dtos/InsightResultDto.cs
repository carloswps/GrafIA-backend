namespace SmartIa.Application.Dtos;

public record InsightItemDto(string Type, string Severity, string Text);

public record InsightResultDto(string? ChartType, List<InsightItemDto> Insights);
