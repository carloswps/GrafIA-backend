using Microsoft.AspNetCore.Mvc;
using SmartIa.Application.Dtos;

namespace SmartIa.Api.Dtos;

public class ProcessReportRequest
{
    public IFormFile File { get; set; } = null!;
    public string? ParametersJson { get; set; }
}

public class ChartRecommendationRequest
{
    public IFormFile File { get; set; } = null!;
}
