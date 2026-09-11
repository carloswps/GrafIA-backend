using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using smart_ia.Dtos;
using smart_ia.Services;

namespace smart_ia.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly DataProcessService _dataProcessor;
    private readonly AiInsightService _aiInsightService;

    public ReportController(AiInsightService aiInsightService, DataProcessService dataProcessService)
    {
        _aiInsightService = aiInsightService;
        _dataProcessor = dataProcessService;
    }

    [HttpPost("proccess")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessReport([FromForm] ProcessReportRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("O arquivo Csv é obrigatorio");

        var parametrs = new ParametersFilterDto(null, null, null, null);
        if (!string.IsNullOrWhiteSpace(request.ParametersJson))
        {
            try
            {
                parametrs = JsonSerializer.Deserialize<ParametersFilterDto>(request.ParametersJson) ?? parametrs;
            }
            catch (JsonException)
            {
                return BadRequest("ParametersJson inválido");
            }
        }

        using var stream = request.File.OpenReadStream();
        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var blocks = new List<BlockReportDto>();



        if (ext == ".xlsx" || ext == ".xls")
        {
            foreach (var (title, labels, values) in _dataProcessor.ProcessDataExcelMulti(stream, parametrs))
            {
                var insight = await _aiInsightService.GenerateInsightAsync(labels, values, parametrs?.ColumnY ?? title);
                blocks.Add(new BlockReportDto(title, DataProcessService.DetectGraphicType(labels, values), labels, new List<DatasetDto> { new(parametrs?.ColumnY ?? "Total", values) }, insight));
            }
        }
        else
        {
            var (labels, values) = _dataProcessor.ProcessDataCsv(stream, parametrs);
            var insight = await _aiInsightService.GenerateInsightAsync(labels, values, parametrs?.ColumnY ?? "Valores");
            blocks.Add(new BlockReportDto("CSV", DataProcessService.DetectGraphicType(labels, values), labels, new List<DatasetDto> { new(parametrs?.ColumnY ?? "Total", values) }, insight));
        }

        if (blocks == null || !blocks.Any())
            return BadRequest(new { message = "Nenhum  bloco do relatório foi gerado para os dados fornecidos" });

        return Ok(new ReportResponseDto(blocks));
    }
}