using Api.Data;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Config;
using WMS.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

var version = builder.Configuration["Application:Version"] ?? "1.0.0";

builder.Services.AddDatabaseConfiguration(builder.Configuration);
builder.Services.AddIdentityConfiguration();
builder.Services.AddAuthenticationConfiguration(builder.Configuration);
builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddRateLimitConfiguration(builder.Configuration);
builder.Services.AddSwaggerConfiguration();
builder.Services.AddApplicationServices();
builder.Services.AddProvidersConfiguration();
builder.Services.AddUploadConfiguration();
builder.Services.AddControllers();
builder.Services.AddValidationConfiguration();
builder.Services.AddMemoryCache();

var app = builder.Build();

// En Docker (o cuando se pide explícitamente) aplica las migraciones pendientes al arrancar,
// para que `docker compose up` deje la base lista sin pasos manuales.
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors(CorsConfiguration.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

// Aviso temprano: sin orígenes configurados el CORS de producción queda cerrado.
if (!app.Environment.IsDevelopment()
    && (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>()).Length == 0)
{
    app.Logger.LogWarning(
        "Cors:AllowedOrigins está vacío: las peticiones cross-origin están bloqueadas en {Environment}.",
        app.Environment.EnvironmentName);
}

app.MapGet("/", () => Results.Ok(new
{
    Application = "Warehouse API",
    Version = version,
    Environment = app.Environment.EnvironmentName,
    Status = "Running"
}));

app.Run();

// Expone el tipo Program a los proyectos de pruebas (WebApplicationFactory<T>).
public partial class Program { }
