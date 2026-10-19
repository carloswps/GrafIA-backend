using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartIa.Application.Exceptions;

namespace SmartIa.Api.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Requisição cancelada pelo cliente em {Path}.", httpContext.Request.Path);
            return true;
        }

        var (status, title) = exception switch
        {
            UnsupportedFileTypeException => (StatusCodes.Status415UnsupportedMediaType, "Tipo de arquivo não suportado"),
            InvalidDatasetException => (StatusCodes.Status422UnprocessableEntity, "Não foi possível processar o arquivo"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Requisição invalida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Erro não tratado em {Path}", httpContext.Request.Path);
        else
            _logger.LogWarning(exception, "Requisição rejeitada ({Status}) em {Path}", status, httpContext.Request.Path);


        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            Instance = httpContext.Request.Path
        }, cancellationToken);


        return true;
    }
}