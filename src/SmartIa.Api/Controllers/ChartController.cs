using Microsoft.AspNetCore.Mvc;
using SmartIa.Application.Dtos;
using SmartIa.Application.UseCases;
using SmartIa.Api.Dtos;

namespace SmartIa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChartController : ControllerBase
{
    private readonly RecommendChartUseCase _recommendChart;

    public ChartController(RecommendChartUseCase recommendChart)
    {
        _recommendChart = recommendChart;
    }

    [HttpPost("recommend")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ChartRecommendationResponseDto>> Recommend([FromForm] ChartRecommendationRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("Arquivo obrigatorio");

        using var stream = request.File.OpenReadStream();
        var result = await _recommendChart.ExecuteAsync(stream, request.File.FileName);

        return Ok(new ChartRecommendationResponseDto(result));
    }
}
