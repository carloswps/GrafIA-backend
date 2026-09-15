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
            Analise a métrica '{{$colY}}' agrupada pela dimensão '{{$colX}}': [{{$dados}}].
            Dados do Ranking: {{$ranking}}.
            Estatísticas Consolidadas: Total={{$total}}, Média={{$media}}, Share Top 1={{$top1}}, Share Top 3={{$top3}} (valores percentuais acumulados).

            DIRETRIZES DE DECISÃO DE GRÁFICO (chartType):
            1. Se houver apenas 1 categoria/registro, use obrigatoriamente "kpi".
            2. Se a dimensão '{{$colX}}' for data, tempo ou período, use obrigatoriamente "line".
            3. Se houver até 5 categorias e a distribuição for equilibrada, prefira "pie".
            4. Nos demais casos ou para comparar rankings, use "bar".

            REGRAS DE RESPOSTA (ESTRITAS):
            - Retorne APENAS um objeto JSON válido, sem marcadores markdown (como ```json) e sem texto explicativo antes ou depois.
            - O campo "chartType" deve ser EXATAMENTE um destes valores: "pie", "bar", "line" ou "kpi".
            - O campo "insight" deve conter no máximo 3 frases com a seguinte estrutura: (1) Quem é o líder e seu valor; (2) O nível de concentração/relevância; (3) Uma sugestão objetiva de ação.
            - NUNCA inclua os símbolos de moeda ou porcentagem (como R$, $, ou %). Escreva apenas os números conforme fornecidos.
            - Se a diferença entre Top1 e Top3 for irrelevante ou inexistente, NÃO mencione o Top3.

            FORMATO DA SAÍDA:
            {"chartType": "bar", "insight": "Frase 1. Frase 2. Frase 3."}
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
        => ranked.Count != 0
            ? $"Destaque principal: '{ranked[0].Label}' lidera com {ranked[0].Value.ToString("N2", culture)}"
            : "Sem dados para análise";
}
