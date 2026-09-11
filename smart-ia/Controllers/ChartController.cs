using Microsoft.AspNetCore.Mvc;
using smart_ia.Dtos;
using smart_ia.Services;

namespace smart_ia.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChartController : ControllerBase
{
    private readonly ChartAiService _chartAiService;
    private readonly DataProcessService _dataProcessService;

    public ChartController(ChartAiService chartAiService, DataProcessService dataProcessService)
    {
        _chartAiService = chartAiService;
        _dataProcessService = dataProcessService;
    }

    [HttpPost("recommend")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ChartRecommendationDto>> Recommend([FromForm] ChartRecommendationRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("Arquivo obrigatorio");


        using var stream = request.File.OpenReadStream();
        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();

        List<Dictionary<string, string>> records = ext == ".xlsx" || ext == ".xls"
        ? _dataProcessService.GetRawRecordsFromExcel(stream)
        : _dataProcessService.GetRawRecordsFromCsv(stream);

        var metadata = _dataProcessService.ExtractMetadata(records);
        var result = await _chartAiService.GetChartRecommendationAsync(metadata);
        return Ok(result);
    }
}