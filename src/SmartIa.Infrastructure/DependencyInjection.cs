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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDataSanitizerService, DataSanitizerService>();
        services.AddScoped<IDataProcessService, DataProcessService>();

        var llmConfig = configuration.GetSection("LLM");
        var apiKey = llmConfig["ApiKey"];
        var modelId = llmConfig["ModelId"];
        var endpoint = llmConfig["Endpoint"];

        var kernelBuilder = services.AddKernel();
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(modelId) && !string.IsNullOrEmpty(endpoint))
            kernelBuilder.AddOpenAIChatCompletion(modelId, new Uri(endpoint), apiKey, httpClient: http);

        services.AddScoped<IAiInsightService, AiInsightService>();
        services.AddScoped<IChartAiService, ChartAiService>();

        return services;
    }
}
