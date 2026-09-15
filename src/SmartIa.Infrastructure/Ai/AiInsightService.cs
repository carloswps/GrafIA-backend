using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Ai;

public class AiInsightService : IAiInsightService
{
    private readonly Kernel _kernel;
    private readonly ILogger<AiInsightService> _logger;
    private readonly IDataSanitizerService _dataSanitizerService;

    public AiInsightService(Kernel kernel, ILogger<AiInsightService> logger, IDataSanitizerService dataSanitizerService)
    {
        _kernel = kernel;
        _logger = logger;
        _dataSanitizerService = dataSanitizerService;
    }

    public async Task<InsightResultDto> GenerateInsightAsync(List<string> labels, List<decimal> values, DataSummaryDto summaryDto)
    {
        if (!labels.Any())
            return new InsightResultDto(null, "Nenhum dado disponível para análise");

        var culture = new System.Globalization.CultureInfo("pt-BR");

        var raw = new Dictionary<string, List<string>> { [summaryDto.ColumnX] = labels };
        var safe = _dataSanitizerService.SanitizeMetaData(raw);

        if (!safe.ContainsKey(summaryDto.ColumnX))
            return new InsightResultDto(null, "Coluna não permitida para análise por IA.");

        var safeSet = new HashSet<string>(safe[summaryDto.ColumnX]);

        var safePairs = labels
            .Zip(values, (l, v) => (Label: l, Value: v))
            .Where(p => safeSet.Contains(p.Label))
            .ToList();

        if (!safePairs.Any())
            return new InsightResultDto(null, "Nenhum dado permitido para análise.");

        var ranked = safePairs.OrderByDescending(p => p.Value).ToList();

        var res = string.Join(", ", ranked.Select(p => $"{p.Label}: {p.Value.ToString("N2", culture)}"));
        var ranking = string.Join(" | ", ranked.Take(3)
            .Select((p, i) => $"#{i + 1} {p.Label}={p.Value.ToString("N2", culture)}"));

        _logger.LogInformation("Gerando insight/grafico para '{ColumnY}' por '{ColumnX}' com dados: {res}",
            summaryDto.ColumnY, summaryDto.ColumnX, res);

        var prompt = """
                        Analise '{{$colY}}' por '{{$colX}}': [{{$dados}}].
                        Ranking: {{$ranking}}.
                        Total={{$total}} Media={{$media}} Top1={{$top1}} Top3={{$top3}} (acumulados, em percentual).
                        Escolha o melhor grafico considerando a quantidade de categorias e a distribuicao.
                        Responda ESTRITAMENTE em JSON, sem texto fora do JSON:
                        {"chartType":"","insight":""}
                        chartType DEVE ser um de: pie, bar, line, kpi. Nunca invente outros valores.
                        insight: maximo 3 frases (lider, concentracao, acao), sem R$, $ ou %,
                        usando apenas os numeros fornecidos. Se Top1 e Top3 forem quase iguais, nao mencione Top3.
                     """;

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0, MaxTokens = 320 };
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

        _logger.LogInformation("Invocando prompt (insight + chartType) para '{ColumnY}' por '{ColumnX}'",
            summaryDto.ColumnY, summaryDto.ColumnX);

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, arguments);
            var json = ExtractJson(result.ToString());

            var parsed = JsonSerializer.Deserialize<InsightResultDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Insight))
                return new InsightResultDto(null, TextFallback(ranked, culture));

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gerar insight via IA para '{ColumnY}' por '{ColumnX}'",
                summaryDto.ColumnY, summaryDto.ColumnX);
            return new InsightResultDto(null, TextFallback(ranked, culture));
        }
    }

    private static string ExtractJson(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        return first >= 0 && last > first ? text[first..(last + 1)] : text.Trim();
    }

    private static string TextFallback(List<(string Label, decimal Value)> ranked, System.Globalization.CultureInfo culture)
        => ranked.Any()
            ? $"Destaque principal: '{ranked[0].Label}' lidera com {ranked[0].Value.ToString("N2", culture)}"
            : "Sem dados para análise";
}
