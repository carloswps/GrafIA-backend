using System.Text.Json;
using Microsoft.SemanticKernel;
using smart_ia.Dtos;

namespace smart_ia.Services;

public class ChartAiService
{
    private readonly Kernel _kernel;

    public ChartAiService(Kernel kernel) { _kernel = kernel; }

    public async Task<ChartRecommendationDto> GetChartRecommendationAsync(DatasetMetadataDto metadata)
    {
        var prompt = """
            Recomende gráfico para '{{$metadata}}'.
            Responda ESTRITAMENTE em JSON: {"chartType":"","columnX":"","columnY":"","title":"","reason":""}
            """;


        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments { ["metadata"] = JsonSerializer.Serialize(metadata) }); var json = result.ToString().Replace("```json", "").Replace("```", "").Trim();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<ChartRecommendationDto>(json, options)
            ?? new ChartRecommendationDto { ChartType = "bar", ColumnX = "", ColumnY = "", Title = "", Reason = "" };
    }
}