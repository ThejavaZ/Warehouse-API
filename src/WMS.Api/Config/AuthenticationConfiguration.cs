using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WMS.Application.Auth;
using WMS.Infrastructure.Auth;

namespace WMS.Api.Config;

public static class AuthenticationConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Autenticación JWT (Bearer) contra la entidad de negocio User:
    /// el login valida con BCrypt y el middleware emite ProblemDetails en 401/403,
    /// igual que el resto de la API.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        // Fail fast: sin secreto no se arranca (nunca se versiona; se fija con
        // user-secrets en dev y con la variable de entorno Auth__Secret en despliegue).
        _ = ReadAuthOptions(configuration);

        services.Configure<AuthenticationOptions>(options =>
        {
            // AddIdentity (registrado antes) fija los esquemas por defecto a las cookies
            // Identity.Application, lo que convertiría los 401 en redirects a /Account/Login
            // y haría que la autenticación se resolviera contra la cookie en vez del JWT.
            // Aquí se anula: autenticación, reto y forzado se resuelven con el esquema Bearer.
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                // Se lee aquí y no en el arranque: las opciones se resuelven en el primer uso,
                // cuando la configuración ya está completa. Así la clave de validación coincide
                // siempre con IOptions<AuthOptions> que usa JwtTokenService para firmar, aunque
                // se amplíe la configuración después de crear el host (p. ej. WebApplicationFactory
                // inyecta Auth:Secret en memoria en los tests de integración).
                var auth = ReadAuthOptions(configuration);

                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.Secret!)),
                    ValidateLifetime = true,
                    // Margen mínimo para relojes ligeramente desincronizados.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // El reto por defecto de JWT devuelve un 401 vacío que no pasa por
                // ErrorHandlingMiddleware; se sustituye para responder ProblemDetails.
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        await WriteAuthProblemAsync(
                            context.Response,
                            StatusCodes.Status401Unauthorized,
                            "unauthorized",
                            "Authentication is required to access this resource.");
                    },
                    OnForbidden = async context =>
                    {
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        await WriteAuthProblemAsync(
                            context.Response,
                            StatusCodes.Status403Forbidden,
                            "forbidden",
                            "You do not have permission to access this resource.");
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    private static AuthOptions ReadAuthOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? new AuthOptions();

        if (string.IsNullOrWhiteSpace(options.Secret) || options.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Falta Auth:Secret (mínimo 32 caracteres). Configúralo con " +
                "`dotnet user-secrets set \"Auth:Secret\" \"<valor de 32+ caracteres>\" --project src/Api` " +
                "o con la variable de entorno Auth__Secret.");
        }

        return options;
    }

    private static async Task WriteAuthProblemAsync(
        HttpResponse response,
        int statusCode,
        string code,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden",
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problem.Extensions["code"] = code;

        response.StatusCode = statusCode;
        response.ContentType = "application/problem+json";
        await response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
