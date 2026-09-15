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
            Recomende a melhor visualização em gráfico com base nos metadados da tabela:
            METADADOS: {{$metadata}}

            REGRAS DE SELEÇÃO:
            - chartType: Deve ser EXATAMENTE um destes valores: "pie", "bar", "line" ou "kpi".
            - columnX: Nome da coluna para o eixo X (dimensão/categoria/data). Deve existir rigorosamente na lista de colunas dos metadados.
            - columnY: Nome da coluna para o eixo Y (métrica/valor numérico). Deve existir rigorosamente na lista de colunas dos metadados.
            - title: Um título curto e descritivo em português para o gráfico (ex: "Vendas por Região").
            - reason: Uma justificativa direta de no máximo 1 frase explicando a escolha do gráfico.

            DIRETRIZES DE TIPO DE GRÁFICO:
            1. Se houver apenas 1 registro ou 1 coluna métrica relevante, escolha "kpi".
            2. Se a coluna selecionada para columnX for do tipo data/tempo, escolha "line".
            3. Se columnX tiver poucas categorias (até 5) com distribuição bem dividida, escolha "pie".
            4. Para comparar categorias, rankings ou cardinalidade maior, escolha "bar".

            REGRAS DE FORMATO (ESTRITAS):
            - Retorne APENAS o JSON válido.
            - NUNCA inclua marcações Markdown como ```json no início ou no fim.
            - NUNCA adicione saudações ou explicações fora do objeto JSON.

            EXEMPLO DE RESPOSTA:
            {"chartType":"bar","columnX":"UF","columnY":"Total","title":"Total de Vendas por Estado","reason":"Gráfico de barras ideal para comparar valores entre diferentes estados."}
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
