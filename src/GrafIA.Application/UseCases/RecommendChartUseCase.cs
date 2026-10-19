using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Application.UseCases;

public class RecommendChartUseCase
{
    private readonly IDataProcessService _dataProcessor;
    private readonly IChartAiService _chartAiService;


    private const int MaxParallelInsightRequests = 4;

    public RecommendChartUseCase(IDataProcessService dataProcessor, IChartAiService chartAiService)
    {
        _dataProcessor = dataProcessor;
        _chartAiService = chartAiService;
    }

    public async Task<List<ChartRecommendationDto>> ExecuteAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var sheets = extension is ".xlsx" or ".xls" ? _dataProcessor.GetRawRecordsFromExcelAll(fileStream).Select(s => BuildMetadata(s.TableName, s.Records)).ToList() : new List<DatasetMetadataDto>
        { BuildMetadata("CSV", _dataProcessor.GetRawRecordsFromCsv(fileStream))};

        var recommendations = new ChartRecommendationDto[sheets.Count];

        using var throttle = new SemaphoreSlim(MaxParallelInsightRequests, MaxParallelInsightRequests);

        var tasks = sheets.Select(async (sheet, index) =>
        {
            await throttle.WaitAsync(cancellationToken);

            try
            {
                var rec = await _chartAiService.GetChartRecommendationAsync(sheet, cancellationToken);
                rec.SheetName = sheet.SheetName;
                recommendations[index] = rec;
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return recommendations.ToList();
    }

    private DatasetMetadataDto BuildMetadata(string sheetName, List<Dictionary<string, string>> records)
    {
        var metadata = _dataProcessor.ExtractMetadata(records);
        metadata.SheetName = sheetName;
        return metadata;
    }
}
