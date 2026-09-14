using System.Text.Json;
using Microsoft.SemanticKernel;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Ai;

public class ChartAiService : IChartAiService
{
    private readonly Kernel _kernel;
    private readonly IDataSanitizerService _dataSanitizerService;

    public ChartAiService(Kernel kernel, IDataSanitizerService dataSanitizerService)
    {
        _kernel = kernel;
        _dataSanitizerService = dataSanitizerService;
    }

    public async Task<ChartRecommendationDto> GetChartRecommendationAsync(DatasetMetadataDto metadata)
    {
        var raw = metadata.Columns.ToDictionary(c => c.Name, c => c.SampleValues);

        var safe = _dataSanitizerService.SanitizeMetaData(raw);
        var safeMetadata = new DatasetMetadataDto
        {
            TotalRows = metadata.TotalRows,
            Columns = safe.Select(kvp => new ColumnSummaryDto
            {
                Name = kvp.Key,
                Type = metadata.Columns.First(c => c.Name == kvp.Key).Type,
                UniqueValuesCount = kvp.Value.Count,
                SampleValues = kvp.Value.Take(3).ToList()
            }).ToList()
        };

        var prompt = """
                     Recomende gráfico para '{{$metadata}}'.
                     Responda ESTRITAMENTE em JSON: {"chartType":"","columnX":"","columnY":"","title":"","reason":""}
                     """;


        var result = await _kernel.InvokePromptAsync(prompt,
            new KernelArguments { ["metadata"] = JsonSerializer.Serialize(safeMetadata) });
        var json = result.ToString().Replace("```json", "").Replace("```", "").Trim();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<ChartRecommendationDto>(json, options)
               ?? new ChartRecommendationDto { ChartType = "bar", ColumnX = "", ColumnY = "", Title = "", Reason = "" };
    }
}
