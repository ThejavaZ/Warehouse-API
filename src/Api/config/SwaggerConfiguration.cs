using System.Reflection;
using Microsoft.OpenApi.Models;

namespace Api.Config;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Warehouse API",
                Version = "v1",
                Description = """
                    API de gestión de almacén.

                    ## Módulo Users
                    - `GET /api/User`: listado paginado con filtros `search`, `isActive`, `page`, `pageSize` y `orderDesc`.
                      Si se envía el parámetro `id`, devuelve el detalle de ese usuario.
                    - `POST /api/User`: crea un usuario (la contraseña se almacena con hash y nunca se devuelve).
                    - `PUT/PATCH /api/User/{id}`: actualiza un usuario.
                    - `DELETE /api/User/{id}`: baja lógica (SoftDelete).

                    ## Errores
                    Todas las respuestas de error usan `application/problem+json` (RFC 7807) con el campo adicional `code`.
                    """
            });

            // Permite probar endpoints protegidos con JWT cuando se agregue autenticación.
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Introduce el token JWT sin el prefijo 'Bearer '."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
