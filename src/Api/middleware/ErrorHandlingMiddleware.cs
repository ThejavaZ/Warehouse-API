using System.Text.Json;

namespace Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Error de negocio conocido → 4xx
            _logger.LogWarning(ex,
                "Error de dominio [{Code}]: {Message}", ex.Code, ex.Message);

            await WriteErrorAsync(context, ex.HttpStatusCode, new ApiError
            {
                Code = ex.Code,
                Message = ex.Message,
                Field = ex.Field
            });
        }
        catch (Exception ex)
        {
            // Error inesperado → 500
            _logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);

            var error = ApiError.InternalError(
                _env.IsDevelopment() ? ex.ToString() : null);

            await WriteErrorAsync(context, 500, error);
        }
    }

    private static async Task WriteErrorAsync(HttpContext ctx, int statusCode, ApiError error)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await ctx.Response.WriteAsync(json);
    }

    private class ApiError
    {
        public string Code { get; set; } = "internal_error";
        public string Message { get; set; } = "An unexpected error occurred.";
        public string? Field { get; set; }
        public string? Details { get; set; }

        public static ApiError InternalError(string? details = null)
        {
            return new ApiError
            {
                Code = "internal_error",
                Message = "An unexpected error occurred.",
                Details = details
            };
        }
    }

    private class DomainException : Exception
    {
        public string Code { get; }
        public string? Field { get; }
        public int HttpStatusCode { get; }

        public DomainException(string code, string message, int httpStatusCode = 400, string? field = null)
            : base(message)
        {
            Code = code;
            HttpStatusCode = httpStatusCode;
            Field = field;
        }
    }
}