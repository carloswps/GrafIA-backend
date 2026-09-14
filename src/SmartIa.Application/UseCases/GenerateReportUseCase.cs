using SmartIa.Application.Domain;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Application.UseCases;

public class GenerateReportUseCase
{
    private readonly IDataProcessService _dataProcessor;
    private readonly IAiInsightService _aiInsightService;

    public GenerateReportUseCase(IDataProcessService dataProcessor, IAiInsightService aiInsightService)
    {
        _dataProcessor = dataProcessor;
        _aiInsightService = aiInsightService;
    }

    public async Task<ReportResponseDto> ExecuteAsync(Stream fileStream, string fileName, ParametersFilterDto filters)
    {
        var extension = GetExtension(fileName);
        var blocks = new List<BlockReportDto>();

        if (extension is ".xlsx" or ".xls")
        {
            foreach (var (title, labels, values, summary) in _dataProcessor.ProcessDataExcelMulti(fileStream, filters))
                blocks.Add(await BuildBlockAsync(title, labels, values, summary));
        }
        else
        {
            var (labels, values, summary) = _dataProcessor.ProcessDataCsv(fileStream, filters);
            blocks.Add(await BuildBlockAsync("CSV", labels, values, summary));
        }

        return new ReportResponseDto(blocks);
    }

    private async Task<BlockReportDto> BuildBlockAsync(
        string title, List<string> labels, List<decimal> values, DataSummaryDto summary)
    {
        var insight = await _aiInsightService.GenerateInsightAsync(labels, values, summary);
        var metricLabel = string.IsNullOrEmpty(summary.ColumnY) ? "Total" : summary.ColumnY;

        return new BlockReportDto(
            title,
            GraphicTypeDetector.Detect(labels, values),
            labels,
            new List<DatasetDto> { new(metricLabel, values) },
            insight);
    }

    private static string GetExtension(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant();
}
