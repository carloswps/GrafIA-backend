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

    public async Task<ChartRecommendationDto> GetChartRecommendationAsync(DatasetMetadataDto metadata, CancellationToken cancellationToken = default)
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
                NUNCA invente outros tipos (ex.: "scatter", "radar", "area", "donut", "treemap", "sankey").
                Se estiver em dúvida, use "bar".
            - columnX: Nome da coluna para o eixo X (dimensão/categoria/data). Deve existir rigorosamente na lista de colunas dos metadados.
            - columnY: Nome da coluna para o eixo Y (métrica/valor numérico). Deve existir rigorosamente na lista de colunas dos metadados.
            - title: Um título curto e descritivo em português para o gráfico (ex: "Vendas por Região").
            - reason: Uma justificativa direta de no máximo 1 frase explicando a escolha do gráfico.

            DIRETRIZES DE TIPO DE GRÁFICO:
                1. Se houver apenas 1 registro ou 1 coluna métrica relevante, escolha "kpi" ou "gauge".
                2. Se a coluna selecionada para columnX for do tipo data/tempo, escolha "line".
                3. Se columnX tiver poucas categorias (até 5) com distribuição bem dividida, escolha "pie".
                4. Se as categorias representarem ETAPAS de um processo (3 a 8), escolha "funnel".
                5. Para comparar categorias, rankings ou cardinalidade maior, escolha "bar".
                6. Use "pictorialBar" apenas como variação visual de "bar" (2 a 10 categorias).

            REGRAS DE FORMATO (ESTRITAS):
            - Retorne APENAS o JSON válido.
            - NUNCA inclua marcações Markdown como ```json no início ou no fim.
            - NUNCA adicione saudações ou explicações fora do objeto JSON.

            EXEMPLO DE RESPOSTA:
            {"chartType":"bar","columnX":"UF","columnY":"Total","title":"Total de Vendas por Estado","reason":"Gráfico de barras ideal para comparar valores entre diferentes estados."}
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt,
                new KernelArguments { ["metadata"] = JsonSerializer.Serialize(safeMetadata) }, cancellationToken: cancellationToken);

            var json = ExtractJson(result.ToString());
            var plan = JsonSerializer.Deserialize<ChartRecommendationDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return ValidatePlan(plan, safeMetadata);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Recomendação de gráfico cancelada para a aba '{Sheet}'.", metadata.SheetName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao recomendar grafico via IA; usando fallback.");
            return Fallback();
        }
    }

    public static ChartRecommendationDto ValidatePlan(ChartRecommendationDto? plan, DatasetMetadataDto metadata)
    {

        if (plan is null) return Fallback();

        var columns = metadata.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(plan.ColumnX) && !columns.ContainsKey(plan.ColumnX))
            plan.ColumnX = string.Empty;

        if (!string.IsNullOrWhiteSpace(plan.ColumnY) && !columns.ContainsKey(plan.ColumnY))
            plan.ColumnY = string.Empty;

        var xCol = !string.IsNullOrWhiteSpace(plan.ColumnX) && columns.TryGetValue(plan.ColumnX, out var c) ? c : null;

        plan.ChartType = xCol is null ? "bar" : GraphicTypeDetector.Resolve(plan.ChartType, xCol.Type, xCol.UniqueValuesCount);

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
