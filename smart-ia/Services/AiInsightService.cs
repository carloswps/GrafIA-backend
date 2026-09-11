using Microsoft.SemanticKernel;

namespace smart_ia.Services;

public class AiInsightService
{
    private readonly Kernel _kernel;

    public AiInsightService(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<string> GenerateInsightAsync(List<string> labels, List<decimal> values, string metricsName)
    {
        if (!labels.Any())
            return "Nenhum dado disponível para análise";

        var res = string.Join(", ", labels.Zip(values, (l, v) => $"{l}: R$ {v:N2}"));

        var prompt = """
            Você é um analista de dados executivo.
            Analise estes dados consolidados da métrica '{{$metrica}}': [{{$dados}}].
            Escreva um insight curto (no máximo 3 frases), destacando o principal indicador, discrepâncias ou sugestão de ação. 
            Seja direto e objetivo, sem introduções genéricas.
            """;

        var arguments = new KernelArguments
        {
            ["metrica"] = metricsName,
            ["dados"] = res
        };

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