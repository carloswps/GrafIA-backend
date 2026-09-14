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

    public async Task<ChartRecommendationDto> ExecuteAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var records = extension is ".xlsx" or ".xls"
            ? _dataProcessor.GetRawRecordsFromExcel(fileStream)
            : _dataProcessor.GetRawRecordsFromCsv(fileStream);

        var metadata = _dataProcessor.ExtractMetadata(records);

        return await _chartAiService.GetChartRecommendationAsync(metadata);
    }
}
