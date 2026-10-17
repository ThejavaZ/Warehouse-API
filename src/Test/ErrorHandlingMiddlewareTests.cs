using System.Text.Json;
using Api.Common;
using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Test;

[TestFixture]
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

    [Test]
    public async Task KeyNotFoundException_Returns404ProblemDetails()
    {
        var (status, contentType, body) = await InvokeAsync(
            _ => throw new KeyNotFoundException("User with ID 42 not found."));

        var problem = ToProblemDetails(body);

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(contentType, Is.EqualTo("application/problem+json"));
            Assert.That(problem!.Status, Is.EqualTo(404));
            Assert.That(problem.Detail, Does.Contain("42"));
            Assert.That(problem.Extensions["code"]?.ToString(), Is.EqualTo("not_found"));
            Assert.That(problem.Extensions.ContainsKey("traceId"), Is.True);
        });
    }

    [Test]
    public async Task ArgumentException_Returns400WithField()
    {
        var (status, _, body) = await InvokeAsync(
            _ => throw new ArgumentException("A user with this email already exists.", "Email"));

        var problem = ToProblemDetails(body);

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem!.Status, Is.EqualTo(400));
            Assert.That(problem.Extensions["code"]?.ToString(), Is.EqualTo("validation_error"));
            Assert.That(problem.Extensions["field"]?.ToString(), Is.EqualTo("Email"));
        });
    }

    [Test]
    public async Task DomainException_UsesItsOwnStatusAndCode()
    {
        var (status, _, body) = await InvokeAsync(
            _ => throw DomainException.Conflict("Email already registered.", "Email"));

        var problem = ToProblemDetails(body);

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("conflict"));
            Assert.That(problem.Extensions["field"]?.ToString(), Is.EqualTo("Email"));
        });
    }

    [Test]
    public async Task UnauthorizedAccessException_Returns403()
    {
        var (status, _, body) = await InvokeAsync(
            _ => throw new UnauthorizedAccessException("Missing scope."));

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(ToProblemDetails(body)!.Extensions["code"]?.ToString(), Is.EqualTo("forbidden"));
        });
    }

    [Test]
    public async Task UnexpectedException_Returns500_WithoutInternalDetails()
    {
        var (status, contentType, body) = await InvokeAsync(
            _ => throw new InvalidOperationException("secret-internal-stacktrace"));

        var problem = ToProblemDetails(body);

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(contentType, Is.EqualTo("application/problem+json"));
            Assert.That(problem!.Detail, Does.Not.Contain("secret-internal-stacktrace"),
                "Los detalles internos no deben filtrarse al cliente.");
            Assert.That(problem.Extensions["code"]?.ToString(), Is.EqualTo("internal_error"));
        });
    }

    [Test]
    public async Task SuccessfulRequest_PassesThroughUnchanged()
    {
        var (status, contentType, body) = await InvokeAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(contentType, Is.Empty);
            Assert.That(body, Is.Empty);
        });
    }
}
