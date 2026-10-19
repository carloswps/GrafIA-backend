using SmartIa.Application.Dtos;

namespace SmartIa.Application.Interfaces;

public interface IChartAiService
{
    Task<ChartRecommendationDto> GetChartRecommendationAsync(DatasetMetadataDto metadata, CancellationToken cancellationToken = default);
}
