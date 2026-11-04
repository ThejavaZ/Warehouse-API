using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WMS.Api.Config;

public static class RateLimitConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Rate limiting con el middleware nativo de .NET 8: ventana fija por dirección IP,
    /// con un límite más estricto para /api/Auth. Las respuestas rechazadas usan
    /// ProblemDetails con código rate_limit_exceeded y cabecera Retry-After.
    /// Los valores se ajustan con la sección "RateLimit" de la configuración.
    /// </summary>
    public static IServiceCollection AddRateLimitConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            // Se lee dentro del delegate (primer uso de las opciones) y no en el arranque:
            // así también se tienen en cuenta los valores que WebApplicationFactory inyecta
            // en memoria después de arrancar el host (tests de integración).
            int permitLimit = configuration.GetValue<int?>("RateLimit:PermitLimit") ?? 100;
            int authPermitLimit = configuration.GetValue<int?>("RateLimit:AuthPermitLimit") ?? 10;
            int windowSeconds = configuration.GetValue<int?>("RateLimit:WindowSeconds") ?? 60;

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;

                if (response.HasStarted)
                {
                    return;
                }

                response.Headers.RetryAfter = windowSeconds.ToString();

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = $"Too many requests. Try again in {windowSeconds} seconds.",
                    Type = "https://httpstatuses.com/429",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["code"] = "rate_limit_exceeded";

                response.ContentType = "application/problem+json";
                await response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions), cancellationToken);
            };

            // `AddPolicy` crea una política con nombre que solo se aplica si el endpoint lleva
            // [EnableRateLimiting("global")]; para que cubra toda la API se fija como límite global.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                // La clave de partición separa los contadores: los intentos de autenticación
                // no consumen la cuota general (ni al revés).
                string partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                bool isAuthEndpoint = httpContext.Request.Path.StartsWithSegments("/api/Auth");

                string key = isAuthEndpoint ? $"auth:{partitionKey}" : $"global:{partitionKey}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = isAuthEndpoint ? authPermitLimit : permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}
