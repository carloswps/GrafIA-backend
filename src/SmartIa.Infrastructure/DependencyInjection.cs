using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SmartIa.Application.Interfaces;
using SmartIa.Infrastructure.Ai;
using SmartIa.Infrastructure.Files;
using SmartIa.Infrastructure.Sanitization;

namespace SmartIa.Infrastructure;

public static class DependencyInjection
{
    private const string LlmHttpClientName = "LlmClient";
    private static readonly TimeSpan LlmTimeout = TimeSpan.FromSeconds(60);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDataSanitizerService>(_ => new DataSanitizerService());
        services.AddScoped<IDataProcessService, DataProcessService>();

        var llmConfig = configuration.GetSection("LLM");
        var apiKey = llmConfig["ApiKey"];
        var modelId = llmConfig["ModelId"];
        var endpoint = llmConfig["Endpoint"];

        var hasLlm = !string.IsNullOrEmpty(apiKey)
                      && !string.IsNullOrEmpty(modelId)
                      && !string.IsNullOrEmpty(endpoint);

        services.AddHttpClient(LlmHttpClientName, client => client.Timeout = LlmTimeout);
        services.AddKernel();

        services.AddScoped<Kernel>(sp =>
        {
            var builder = Kernel.CreateBuilder();
            if (hasLlm)
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                builder.AddOpenAIChatCompletion(
                     modelId!, new Uri(endpoint!), apiKey!,
                    httpClient: factory.CreateClient(LlmHttpClientName));
            }
            return builder.Build();
        });

        services.AddScoped<IAiInsightService, AiInsightService>();
        services.AddScoped<IChartAiService, ChartAiService>();

        return services;
    }
}
