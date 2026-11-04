using System.Text.Json;
using Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace WMS.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request {Path} was cancelled by the client.",
                context.Request.Path);
            throw;
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain error [{Code}] on {Path}", ex.Code, context.Request.Path);

            await WriteProblemAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Field);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found on {Path}", context.Request.Path);

            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "not_found", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument on {Path}", context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "validation_error",
                WithoutParameterSuffix(ex),
                ex.ParamName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access on {Path}", context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "forbidden",
                ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning(ex, "Malformed request on {Path}", context.Request.Path);

            await WriteProblemAsync(
                context,
                ex.StatusCode,
                "bad_request",
                "The request payload is invalid.");
        }
        catch (Exception ex)
        {
            // Los detalles internos quedan solo en el log, nunca en la respuesta.
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string code,
        string detail,
        string? field = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (!string.IsNullOrEmpty(field))
        {
            problem.Extensions["field"] = field;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    /// <summary>
    /// ArgumentException le añade al mensaje un sufijo interno de .NET
    /// (<c>(Parameter 'SKU')</c>). Ese <c>detail</c> es lo que acaba viendo el
    /// usuario final en el cliente, así que se quita: el nombre del campo ya
    /// viaja aparte en la extensión <c>field</c>.
    /// </summary>
    private static string WithoutParameterSuffix(ArgumentException ex)
    {
        if (string.IsNullOrEmpty(ex.ParamName))
        {
            return ex.Message;
        }

        var suffix = $"(Parameter '{ex.ParamName}')";
        var message = ex.Message.EndsWith(suffix, StringComparison.Ordinal)
            ? ex.Message[..^suffix.Length].TrimEnd()
            : ex.Message;

        // Sin mensaje propio quedaría un detail vacío, peor que el sufijo.
        return string.IsNullOrWhiteSpace(message)
            ? $"Invalid value for '{ex.ParamName}'."
            : message;
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status413PayloadTooLarge => "Payload Too Large",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        _ => "Error"
    };
}
