using Microsoft.AspNetCore.Mvc;
using SmartIa.Application.Dtos;
using SmartIa.Application.UseCases;
using SmartIa.Api.Dtos;
using SmartIa.Api.Validation;

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
    [ProducesResponseType<ChartRecommendationResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ChartRecommendationResponseDto>> Recommend([FromForm] ChartRecommendationRequest request)
    {

        FileUploadValidator.EnsureValid(request.File);

        using var stream = request.File.OpenReadStream();
        var result = await _recommendChart.ExecuteAsync(stream, request.File.FileName, HttpContext.RequestAborted);
        return Ok(new ChartRecommendationResponseDto(result));
    }
}
