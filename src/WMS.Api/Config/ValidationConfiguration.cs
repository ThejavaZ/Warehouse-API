using Microsoft.AspNetCore.Mvc;

namespace WMS.Api.Config;

public static class ValidationConfiguration
{
    /// <summary>
    /// Unifica las respuestas de validación automática de [ApiController] (ModelState inválido)
    /// para que también usen ProblemDetails (RFC 7807) con content type application/problem+json,
    /// igual que las respuestas generadas por ErrorHandlingMiddleware.
    /// </summary>
    public static IServiceCollection AddValidationConfiguration(
        this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Type = "https://httpstatuses.com/400",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["code"] = "validation_error";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return services;
    }
}
