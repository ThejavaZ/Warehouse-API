using Api.Config;
using Api.Data;
using Api.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var version = builder.Configuration["Application:Version"] ?? "1.0.0";

builder.Services.AddDatabaseConfiguration(builder.Configuration);
builder.Services.AddIdentityConfiguration();
builder.Services.AddCorsConfiguration();
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
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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