using Api.Config;
using Api.Middleware;

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
builder.Services.AddMemoryCache();

var app = builder.Build();

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