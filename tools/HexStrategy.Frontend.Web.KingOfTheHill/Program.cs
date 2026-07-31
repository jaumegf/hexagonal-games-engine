using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});

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
