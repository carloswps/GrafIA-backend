using Microsoft.Extensions.DependencyInjection;
using SmartIa.Application.Interfaces;
using SmartIa.Application.UseCases;

namespace SmartIa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GenerateReportUseCase>();
        services.AddScoped<RecommendChartUseCase>();
        return services;
    }
}
