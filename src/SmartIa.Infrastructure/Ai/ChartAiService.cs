using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SmartIa.Application.Domain;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Ai;

public class ChartAiService : IChartAiService
{

    private readonly Kernel _kernel;
    private readonly ILogger<ChartAiService> _logger;
    private readonly IDataSanitizerService _dataSanitizerService;

    public ChartAiService(Kernel kernel, ILogger<ChartAiService> logger, IDataSanitizerService dataSanitizerService)
    {
        _kernel = kernel;
        _logger = logger;
        _dataSanitizerService = dataSanitizerService;
    }

    public async Task<ChartRecommendationDto> GetChartRecommendationAsync(DatasetMetadataDto metadata)
    {
        var raw = metadata.Columns.ToDictionary(c => c.Name, c => c.SampleValues);

        var safe = _dataSanitizerService.SanitizeMetaData(raw);

        var original = metadata.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var safeMetadata = new DatasetMetadataDto
        {
            SheetName = metadata.SheetName,
            TotalRows = metadata.TotalRows,
            Columns = safe.Select(kvp => new ColumnSummaryDto
            {
                Name = kvp.Key,
                Type = original[kvp.Key].Type,
                UniqueValuesCount = original[kvp.Key].UniqueValuesCount,
                SampleValues = kvp.Value.Take(3).ToList()
            }).ToList()
        };

        var prompt = """
                     Recomende gráfico para '{{$metadata}}'.
                     Responda ESTRITAMENTE em JSON: {"chartType":"","columnX":"","columnY":"","title":"","reason":""}
                     chartType DEVE ser um de: pie, bar, line, kpi.
                     columnX e columnY DEVEM ser nomes existentes na lista de colunas fornecida.
                     """;

        var allowedColumns = safeMetadata.Columns
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt,
                new KernelArguments { ["metadata"] = JsonSerializer.Serialize(safeMetadata) });

            var json = ExtractJson(result.ToString());
            var plan = JsonSerializer.Deserialize<ChartRecommendationDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ValidatePlan(plan, allowedColumns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao recomendar grafico via IA; usando fallback.");
            return Fallback();
        }
    }

    public static ChartRecommendationDto ValidatePlan(ChartRecommendationDto? plan, IReadOnlySet<string> allowedColumns)
    {
        if (plan is null) return Fallback();

        if (!GraphicTypeDetector.KnownTypes.Contains(plan.ChartType))
            plan.ChartType = "bar";

        if (!string.IsNullOrWhiteSpace(plan.ColumnX) && !allowedColumns.Contains(plan.ColumnX))
            plan.ColumnX = string.Empty;

        if (!string.IsNullOrWhiteSpace(plan.ColumnY) && !allowedColumns.Contains(plan.ColumnY))
            plan.ColumnY = string.Empty;

        return plan;
    }

    private static ChartRecommendationDto Fallback() =>
        new() { ChartType = "bar", ColumnX = "", ColumnY = "", Title = "", Reason = "" };

    private static string ExtractJson(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        return first >= 0 && last > first ? text[first..(last + 1)] : text.Trim();
    }
}