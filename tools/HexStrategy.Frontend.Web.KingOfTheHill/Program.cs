using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/config.js", (IConfiguration configuration) =>
{
    var payload = new
    {
        backendBaseUrl = configuration["Frontend:BackendBaseUrl"] ?? "http://localhost:5091",
        gameDefinitionId = "king-of-the-hill"
    };

    return Results.Text(
        $"window.kingOfTheHillConfig = {JsonSerializer.Serialize(payload)};",
        "application/javascript");
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.Run();
