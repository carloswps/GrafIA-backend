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

    public async Task<string> GenerateInsightAsync(List<string> labels, List<decimal> values, DataSummaryDto summaryDto)
    {
        if (!labels.Any())
            return "Nenhum dado disponível para análise";

        var culture = new System.Globalization.CultureInfo("pt-BR");

        var raw = new Dictionary<string, List<string>> { [summaryDto.ColumnX] = labels };
        var safe = _dataSanitizerService.SanitizeMetaData(raw);

        if (!safe.ContainsKey(summaryDto.ColumnX)) return "Coluna não permitida para análise por IA.";

        var safeSet = new HashSet<string>(safe[summaryDto.ColumnX]);

        var safePairs = labels
            .Zip(values, (l, v) => (Label: l, Value: v))
            .Where(p => safeSet.Contains(p.Label))
            .ToList();

        if (!safePairs.Any()) return "Nenhum dado permitido para análise.";

        var res = string.Join(", ", safePairs.Select(p => $"{p.Label}: {p.Value.ToString("N2", culture)}"));
        _logger.LogInformation("Gerando insight para a metrica '{ColumnY}' por '{ColumnX}' com dados: {res}",
            summaryDto.ColumnY, summaryDto.ColumnX, res);


        var prompt = """
                        Analise '{{$colY}}' por '{{$colX}}': [{{$dados}}].
                        Total={{$total}} Media={{$media}} Top1={{$top1}}% Top3={{$top3}}%.
                        Regras: nao assuma moeda. Nao use R$, $ ou %. Use apenas numeros fornecidos. Maximo 3 frases: lider, concentracao, acao.
                     """;

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0, MaxTokens = 220 };
        var arguments = new KernelArguments(settings)
        {
            ["colX"] = summaryDto.ColumnX,
            ["colY"] = summaryDto.ColumnY,
            ["dados"] = res,
            ["total"] = summaryDto.Total.ToString("N2", culture),
            ["media"] = summaryDto.Media.ToString("N2", culture),
            ["top1"] = summaryDto.ShareTop1.ToString("N2", culture),
            ["top3"] = summaryDto.ShareTop3.ToString("N2", culture)
        };

        _logger.LogInformation("Invocando prompt para gerar insight para a metrica '{ColumnY}' por '{ColumnX}'",
            summaryDto.ColumnY, summaryDto.ColumnX);

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, arguments);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gerar insight via IA para '{ColumnY}' por '{ColumnX}'",
        summaryDto.ColumnY, summaryDto.ColumnX);
            return $"Destaque principal: '{safePairs[0].Label}' lidera com {safePairs[0].Value:N2}";
        }
    }
}
