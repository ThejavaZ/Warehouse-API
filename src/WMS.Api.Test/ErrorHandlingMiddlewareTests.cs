using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WMS.Api.Middleware;
using WMS.Domain.Common;

namespace WMS.Api.Test;

public class ErrorHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<(int StatusCode, string ContentType, string Body)> InvokeAsync(
        RequestDelegate next)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlingMiddleware(
            next,
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return (context.Response.StatusCode, context.Response.ContentType ?? string.Empty, body);
    }

    private static ProblemDetails? ToProblemDetails(string body)
        => JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);

    [Fact]
    public async Task KeyNotFoundException_Returns404ProblemDetails()
    {
        var (status, contentType, body) = await InvokeAsync(
            _ => throw new KeyNotFoundException("User with ID 42 not found."));

        var problem = ToProblemDetails(body);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("application/problem+json", contentType);
        Assert.Equal(404, problem!.Status);
        Assert.Contains("42", problem.Detail);
        Assert.Equal("not_found", problem.Extensions["code"]?.ToString());
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task ArgumentException_Returns400WithField()
    {
        var (status, _, body) = await InvokeAsync(
            _ => throw new ArgumentException("A user with this email already exists.", "Email"));

        var problem = ToProblemDetails(body);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("validation_error", problem.Extensions["code"]?.ToString());
        Assert.Equal("Email", problem.Extensions["field"]?.ToString());
        // El detalle es lo que ve el usuario final: sin el sufijo interno
        // de .NET "(Parameter 'Email')", que ya viaja en `field`.
        Assert.Equal("A user with this email already exists.", problem.Detail);
    }

    [Fact]
    public async Task ArgumentException_WithoutMessage_StillProducesADetail()
    {
        var (status, _, body) = await InvokeAsync(_ => throw new ArgumentException(null, "SKU"));

        var problem = ToProblemDetails(body);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        // Sin mensaje propio, .NET rellena con un recurso localizado: solo
        // se exige que llegue algo y que no traiga el sufijo del parámetro,
        // que es lo que no debe ver el usuario final.
        Assert.False(string.IsNullOrEmpty(problem!.Detail));
        Assert.DoesNotContain("(Parameter", problem.Detail);
        Assert.Equal("SKU", problem.Extensions["field"]?.ToString());
    }

    [Fact]
    public async Task DomainException_UsesItsOwnStatusAndCode()
    {
        var (status, _, body) = await InvokeAsync(
            _ => throw DomainException.Conflict("Email already registered.", "Email"));

        var problem = ToProblemDetails(body);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("conflict", problem!.Extensions["code"]?.ToString());
        Assert.Equal("Email", problem.Extensions["field"]?.ToString());
    }

    [Fact]
    public async Task UnauthorizedAccessException_Returns403()
    {
        var (status, _, body) = await InvokeAsync(
            _ => throw new UnauthorizedAccessException("Missing scope."));

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("forbidden", ToProblemDetails(body)!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500_WithoutInternalDetails()
    {
        var (status, contentType, body) = await InvokeAsync(
            _ => throw new InvalidOperationException("secret-internal-stacktrace"));

        var problem = ToProblemDetails(body);

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("application/problem+json", contentType);
        // Los detalles internos no deben filtrarse al cliente.
        Assert.DoesNotContain("secret-internal-stacktrace", problem!.Detail);
        Assert.Equal("internal_error", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task SuccessfulRequest_PassesThroughUnchanged()
    {
        var (status, contentType, body) = await InvokeAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(contentType);
        Assert.Empty(body);
    }
}
