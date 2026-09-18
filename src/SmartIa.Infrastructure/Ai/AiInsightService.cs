using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Ai;

public class AiInsightService : IAiInsightService
{
    private const int MaxInsights = 3;

    private static readonly HashSet<string> KnownTypes =
        new(StringComparer.OrdinalIgnoreCase) { "leader", "concentration", "trend", "outlier", "action" };

    private static readonly HashSet<string> KnownSeverities =
        new(StringComparer.OrdinalIgnoreCase) { "info", "warning", "critical" };

    private readonly Kernel _kernel;
    private readonly ILogger<AiInsightService> _logger;
    private readonly IDataSanitizerService _dataSanitizerService;

    public AiInsightService(Kernel kernel, ILogger<AiInsightService> logger, IDataSanitizerService dataSanitizerService)
    {
        _kernel = kernel;
        _logger = logger;
        _dataSanitizerService = dataSanitizerService;
    }

    public async Task<InsightResultDto> GenerateInsightAsync(List<string> labels, List<decimal> values, DataSummaryDto summaryDto, CancellationToken cancellationToken = default)
    {
        if (!labels.Any())
            return new InsightResultDto(null, new List<InsightItemDto>());

        var culture = new System.Globalization.CultureInfo("pt-BR");

        var raw = new Dictionary<string, List<string>> { [summaryDto.ColumnX] = labels };
        var safe = _dataSanitizerService.SanitizeMetaData(raw);

        if (!safe.ContainsKey(summaryDto.ColumnX))
            return new InsightResultDto(null, new List<InsightItemDto>());

        var safeSet = new HashSet<string>(safe[summaryDto.ColumnX]);

        var safePairs = labels
            .Zip(values, (l, v) => (Label: l, Value: v))
            .Where(p => safeSet.Contains(p.Label))
            .ToList();

        if (!safePairs.Any())
            return new InsightResultDto(null, new List<InsightItemDto>());

        var ranked = safePairs.OrderByDescending(p => p.Value).ToList();

        var res = string.Join(", ", ranked.Select(p => $"{p.Label}: {p.Value.ToString("N2", culture)}"));
        var ranking = string.Join(" | ", ranked.Take(3)
            .Select((p, i) => $"#{i + 1} {p.Label}={p.Value.ToString("N2", culture)}"));

        _logger.LogInformation("Gerando insights para '{ColumnY}' por '{ColumnX}' com dados: {res}",
            summaryDto.ColumnY, summaryDto.ColumnX, res);

        var prompt = """
            Analise a métrica '{{$colY}}' agrupada pela dimensão '{{$colX}}': [{{$dados}}].
            Dados do Ranking: {{$ranking}}.
            Estatísticas Consolidadas: Total={{$total}}, Média={{$media}}, Share Top 1={{$top1}}, Share Top 3={{$top3}} (percentuais acumulados).

            DIRETRIZES DE DECISÃO DE GRÁFICO (chartType):
            1. Se houver apenas 1 categoria/registro, use obrigatoriamente "kpi".
            2. Se a dimensão '{{$colX}}' for data, tempo ou período, use obrigatoriamente "line".
            3. Se houver até 5 categorias e a distribuição for equilibrada, prefira "pie".
            4. Nos demais casos ou para comparar rankings, use "bar".

            GERAÇÃO DE INSIGHTS:
            Gere NO MÁXIMO 3 insights, escolhendo os ângulos aplicáveis NESTA ORDEM de prioridade:
            1. "leader"        — quem lidera e seu valor (sempre aplicável).
            2. "concentration" — nível de concentração (share Top1/Top3); aplicável se houver mais de 1 categoria.
            3. "trend"         — evolução ao longo do tempo; SOMENTE se '{{$colX}}' for data/tempo.
            4. "outlier"       — valor muito discrepante dos demais; SOMENTE se houver discrepância clara.
            5. "action"        — sugestão objetiva de ação (sempre aplicável).

            Pare ao atingir 3 insights. NÃO invente ângulos sem suporte nos dados.
            Cada insight deve ter no máximo 2 frases, sem símbolos de moeda ou porcentagem (R$, $, %).

            REGRAS DE RESPOSTA (ESTRITAS):
            - Retorne APENAS um objeto JSON válido, sem marcadores markdown (como ```json) e sem texto antes ou depois.
            - "chartType" deve ser EXATAMENTE: "pie", "bar", "line", "kpi", "funnel", "gauge" ou "pictorialBar".
            - "severity" deve ser EXATAMENTE: "info", "warning" ou "critical".

            FORMATO DA SAÍDA:
            {"chartType":"bar","insights":[{"type":"leader","severity":"info","text":"Frase."},{"type":"action","severity":"info","text":"Frase."}]}
            """;

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0, MaxTokens = 600 };
        var arguments = new KernelArguments(settings)
        {
            ["colX"] = summaryDto.ColumnX,
            ["colY"] = summaryDto.ColumnY,
            ["dados"] = res,
            ["ranking"] = ranking,
            ["total"] = summaryDto.Total.ToString("N2", culture),
            ["media"] = summaryDto.Media.ToString("N2", culture),
            ["top1"] = summaryDto.ShareTop1.ToString("N2", culture),
            ["top3"] = summaryDto.ShareTop3.ToString("N2", culture)
        };

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, arguments, cancellationToken: cancellationToken);
            var json = ExtractJson(result.ToString());

            var parsed = JsonSerializer.Deserialize<InsightResultDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return Validate(parsed, ranked, summaryDto, culture);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Geração de insights cancelada para '{ColumnY}' por '{ColumnX}'.", summaryDto.ColumnY, summaryDto.ColumnY);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gerar insights via IA para '{ColumnY}' por '{ColumnX}'",
                summaryDto.ColumnY, summaryDto.ColumnX);
            return new InsightResultDto(null, BuildFallback(ranked, summaryDto, culture));
        }
    }

    private static InsightResultDto Validate(
        InsightResultDto? parsed,
        List<(string Label, decimal Value)> ranked,
        DataSummaryDto summary,
        System.Globalization.CultureInfo culture)
    {
        var items = parsed?.Insights ?? new List<InsightItemDto>();

        items = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Text))
            .Select(i => new InsightItemDto(
                Type: KnownTypes.Contains(i.Type) ? i.Type.ToLowerInvariant() : "info",
                Severity: KnownSeverities.Contains(i.Severity) ? i.Severity.ToLowerInvariant() : "info",
                Text: i.Text.Trim()))
            .Where(i => KnownTypes.Contains(i.Type))
            .Take(MaxInsights)
            .ToList();

        var fallback = BuildFallback(ranked, summary, culture);

        if (!items.Any(i => i.Type == "leader"))
            items.Insert(0, fallback.First(i => i.Type == "leader"));

        if (!items.Any(i => i.Type == "action"))
            items.Add(fallback.First(i => i.Type == "action"));

        items = items.Take(MaxInsights).ToList();

        return new InsightResultDto(parsed?.ChartType, items);
    }

    private static List<InsightItemDto> BuildFallback(
        List<(string Label, decimal Value)> ranked,
        DataSummaryDto summary,
        System.Globalization.CultureInfo culture)
    {
        var list = new List<InsightItemDto>();

        if (ranked.Count == 0)
            return list;

        var leader = ranked[0];
        list.Add(new InsightItemDto("leader", "info",
            $"'{leader.Label}' lidera com {leader.Value.ToString("N2", culture)}."));

        if (ranked.Count > 1)
            list.Add(new InsightItemDto("concentration", "info",
                $"O Top 1 representa {summary.ShareTop1.ToString("N2", culture)} e o Top 3 {summary.ShareTop3.ToString("N2", culture)} do total."));

        list.Add(new InsightItemDto("action", "info",
            $"Priorize ações sobre '{leader.Label}', que concentra o maior volume."));

        return list.Take(MaxInsights).ToList();
    }

    private static string ExtractJson(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        return first >= 0 && last > first ? text[first..(last + 1)] : text.Trim();
    }
}