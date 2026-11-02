namespace Api.Config;

public static class CorsConfiguration
{
    public const string PolicyName = "CorsPolicy";

    /// <summary>
    /// En Development se permite cualquier origen (comodidad de desarrollo).
    /// En el resto de entornos solo se admiten los orígenes de "Cors:AllowedOrigins":
    /// si la lista está vacía, no se habilita ningún origen externo (más seguro que AllowAll).
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // `Get<string[]>()` parte por comas: con la variable vacía devuelve [""], así que se
        // descartan los huecos y solo quedan orígenes reales (vacío = sin orígenes externos).
        string[] allowedOrigins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? Array.Empty<string>())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, builder =>
            {
                if (environment.IsDevelopment())
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
                else
                {
                    // Sin orígenes configurados no se añade ninguna regla de origen:
                    // las peticiones cross-origin quedan bloqueadas por defecto.
                    builder.WithOrigins(allowedOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
            });
        });

        return services;
    }
}
