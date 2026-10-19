using SmartIa.Application.Domain;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Application.UseCases;

public class GenerateReportUseCase
{
    private const int MaxParallelInsightRequests = 4;

    private readonly IDataProcessService _dataProcessor;
    private readonly IAiInsightService _aiInsightService;

    public GenerateReportUseCase(IDataProcessService dataProcessor, IAiInsightService aiInsightService)
    {
        _dataProcessor = dataProcessor;
        _aiInsightService = aiInsightService;
    }

    public async Task<ReportResponseDto> ExecuteAsync(Stream fileStream, string fileName, ParametersFilterDto filters, CancellationToken cancellationToken = default)
    {
        var extension = GetExtension(fileName);

        var results = extension is ".xlsx" or ".xls"
            ? _dataProcessor.ProcessDataExcelMulti(fileStream, filters)
            : new List<ProcessResultDto> { _dataProcessor.ProcessDataCsv(fileStream, filters) };

        var blocks = await BuildBlocksAsync(results, cancellationToken);

        var inferredStart = results.Select(r => r.PeriodStart).Where(d => d.HasValue).Min();
        var inferredEnd = results.Select(r => r.PeriodEnd).Where(d => d.HasValue).Max();

        var context = new ReportContextDto(
            RowsProcessed: results.Sum(r => r.RowsRead),
            GeneratedAt: DateTime.UtcNow,
            PeriodStart: filters.DateIni ?? inferredStart,
            PeriodEnd: filters.DateEnd ?? inferredEnd,
            AppliedFilters: filters
        );

        return new ReportResponseDto(blocks, context);
    }

    private async Task<BlockReportDto> BuildBlockAsync(ProcessResultDto result, int index, CancellationToken cancellationToken)
    {
        var labels = result.Labels;
        var values = result.Values;
        var summary = result.Summary;

        var analysis = await _aiInsightService.GenerateInsightAsync(labels, values, summary, cancellationToken);
        var metricLabel = string.IsNullOrEmpty(summary.ColumnY) ? "Total" : summary.ColumnY;

        var leaderIdx = values.Count > 0 ? values.IndexOf(values.Max()) : -1;
        var leaderLabel = leaderIdx >= 0 && leaderIdx < labels.Count ? labels[leaderIdx] : null;

        var topItems = labels
            .Zip(values, (label, value) => new { Label = label, Value = value })
            .OrderByDescending(x => x.Value)
            .Take(3)
            .Select((x, i) => new TopItemDto(
                Rank: i + 1,
                Label: x.Label,
                Value: x.Value,
                Share: summary.Total > 0 ? (double)(x.Value / summary.Total * 100) : 0))
            .ToList();

        var kpis = new BlockKpisDto(
            Total: summary.Total,
            Media: summary.Media,
            Max: summary.Max,
            Min: summary.Min,
            Count: values.Count,
            ShareTop1: summary.ShareTop1,
            ShareTop3: summary.ShareTop3,
            LeaderLabel: leaderLabel,
            LeaderValue: leaderIdx >= 0 ? values[leaderIdx] : 0m,
            TopItems: topItems,
            GroupsShown: summary.GroupsShown,
            TotalGroups: summary.TotalGroups,
            IsTruncated: summary.IsTruncated
        );

        return new BlockReportDto(
            Id: $"block-{index}",
            Title: string.IsNullOrWhiteSpace(result.Title) || result.Title == "CSV" ? metricLabel : result.Title,
            TypeGraphic: GraphicTypeDetector.Resolve(analysis.ChartType, labels, values),

            Dimension: summary.ColumnX,
            Metric: metricLabel,
            Unit: UnitInference.Infer(metricLabel),
            Labels: labels,
            Datasets: new List<DatasetDto> { new(metricLabel, values) },
            Kpis: kpis,
            InsightsIa: analysis.Insights
        );
    }

    private async Task<List<BlockReportDto>> BuildBlocksAsync(List<ProcessResultDto> results, CancellationToken cancellationToken)
    {
        var blocks = new BlockReportDto[results.Count];

        using var throttle = new SemaphoreSlim(MaxParallelInsightRequests, MaxParallelInsightRequests);

        var tasks = results.Select(async (result, index) =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                blocks[index] = await BuildBlockAsync(result, index, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return blocks.ToList();
    }

    private static string GetExtension(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant();
}
