using SmartIa.Application.Dtos;

namespace SmartIa.Application.Interfaces;

public interface IAiInsightService
{
    Task<InsightResultDto> GenerateInsightAsync(List<string> labels, List<decimal> values, DataSummaryDto summaryDto, CancellationToken cancellationToken = default);
}
