using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartIa.Application.Dtos;
using SmartIa.Application.UseCases;
using SmartIa.Api.Dtos;
using SmartIa.Api.Validation;

namespace SmartIa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly GenerateReportUseCase _generateReport;

    public ReportController(GenerateReportUseCase generateReport)
    {
        _generateReport = generateReport;
    }

    [HttpPost("process")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ReportResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ReportResponseDto>> ProcessReport([FromForm] ProcessReportRequest request)
    {
        FileUploadValidator.EnsureValid(request.File);

        var filters = new ParametersFilterDto(null, null, null, null);
        if (!string.IsNullOrWhiteSpace(request.ParametersJson))
            try
            {
                filters = JsonSerializer.Deserialize<ParametersFilterDto>(request.ParametersJson) ?? filters;
            }
            catch (JsonException)
            {
                return BadRequest("ParametersJson inválido");
            }

        using var stream = request.File.OpenReadStream();
        var result = await _generateReport.ExecuteAsync(stream, request.File.FileName, filters, HttpContext.RequestAborted);

        if (result.Blocks == null || result.Blocks.Count == 0)
            return BadRequest(new { message = "Nenhum bloco do relatório foi gerado para os dados fornecidos" });

        return Ok(result);
    }
}