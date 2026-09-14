using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using smart_ia.Dtos;

namespace smart_ia.Services;

public class AiInsightService
{
    private readonly Kernel _kernel;
    private readonly ILogger<AiInsightService> _logger;

    public AiInsightService(Kernel kernel, ILogger<AiInsightService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<string> GenerateInsightAsync(List<string> labels, List<decimal> values, DataSummaryDto summaryDto)
    {
        if (!labels.Any())
            return "Nenhum dado disponível para análise";

        var culture = new System.Globalization.CultureInfo("pt-BR");
        var res = string.Join(", ", labels.Zip(values, (l, v) => $"{l}: {v.ToString("N2", culture)}"));

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
        _logger.LogInformation("Dados gerados: {res}", res);

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, arguments);
            return result.ToString();
        }
        catch (Exception)
        {
            return $"Destaque principal: '{labels.FirstOrDefault()}' lidera com {values.FirstOrDefault():N2}.";
        }
    }
}