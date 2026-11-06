using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WMS.Infrastructure.Persistence;

namespace Api.Test.Integration;

/// <summary>
/// Arranca la API completa en memoria: reemplaza PostgreSQL por SQLite en memoria,
/// crea el esquema desde el modelo de EF Core y fija la configuración de prueba
/// (secreto JWT en memoria y límites de rate limiting altos para no afectar a la suite).
/// </summary>
public class WarehouseApiFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecret = "integration-test-secret-with-at-least-32-characters";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting escribe en la configuración del host, que WebApplicationBuilder
        // fusiona antes de ejecutar Program.cs: así el fail fast de
        // AddAuthenticationConfiguration ve el secreto aunque no existan user secrets
        // (que es exactamente lo que ocurre en CI).
        builder.UseSetting("Auth:Secret", TestJwtSecret);
        builder.UseSetting("RateLimit:PermitLimit", "1000000");
        builder.UseSetting("RateLimit:AuthPermitLimit", "1000000");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Secret"] = TestJwtSecret,
                ["RateLimit:PermitLimit"] = "1000000",
                ["RateLimit:AuthPermitLimit"] = "1000000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // EF Core 8.0.0 registra las opciones del DbContext por estos descriptores.
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(DbContextOptions));

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
