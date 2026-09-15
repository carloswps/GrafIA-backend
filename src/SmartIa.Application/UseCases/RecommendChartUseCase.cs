using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Application.UseCases;

public class RecommendChartUseCase
{
    private readonly IDataProcessService _dataProcessor;
    private readonly IChartAiService _chartAiService;

    public RecommendChartUseCase(IDataProcessService dataProcessor, IChartAiService chartAiService)
    {
        _dataProcessor = dataProcessor;
        _chartAiService = chartAiService;
    }

    public async Task<List<ChartRecommendationDto>> ExecuteAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var sheets = extension is ".xlsx" or ".xls" ? _dataProcessor.GetRawRecordsFromExcelAll(fileStream).Select(s => BuildMetadata(s.TableName, s.Records)).ToList() : new List<DatasetMetadataDto>
        { BuildMetadata("CSV", _dataProcessor.GetRawRecordsFromCsv(fileStream))};

        var recommendations = new List<ChartRecommendationDto>();

        foreach (var sheet in sheets)
        {
            var rec = await _chartAiService.GetChartRecommendationAsync(sheet);
            rec.SheetName = sheet.SheetName;
            recommendations.Add(rec);
        }
        return recommendations;
    }

    private DatasetMetadataDto BuildMetadata(string sheetName, List<Dictionary<string, string>> records)
    {
        var metadata = _dataProcessor.ExtractMetadata(records);
        metadata.SheetName = sheetName;
        return metadata;
    }
}
