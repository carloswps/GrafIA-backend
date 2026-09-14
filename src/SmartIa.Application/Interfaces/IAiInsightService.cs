using SmartIa.Application.Dtos;

namespace SmartIa.Application.Interfaces;

public interface IAiInsightService
{
    Task<string> GenerateInsightAsync(List<string> labels, List<decimal> values, DataSummaryDto summaryDto);
}
