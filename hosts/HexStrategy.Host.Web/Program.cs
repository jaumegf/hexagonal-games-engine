using System.Text.Json.Serialization;
using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;
using HexStrategy.Core.Contracts;
using HexStrategy.Game.KingOfTheHill;
using HexStrategy.Session.Matches;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IGameDefinition, KingOfTheHillGameDefinition>();
builder.Services.AddSingleton<GameCatalog>();
builder.Services.AddSingleton<GameMatchService>();
builder.Services.AddSingleton<HexStrategy.Session.Sessions.GameSessionRegistry>();
builder.Services.AddSingleton<ActiveGameMatchRegistry>();

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    Service = "HexStrategy.Host.Web",
    Message = "Use /api for the backend endpoints."
}));

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.MapGet("/api", (GameCatalog catalog) => Results.Ok(new ApiRootResponse(
    "HexStrategy.Host.Web",
    catalog.Definitions
        .OrderBy(definition => definition.Metadata.DisplayName)
        .Select(definition => new GameDefinitionResponse(
            definition.Metadata.Id,
            definition.Metadata.DisplayName))
        .ToArray())));

app.MapPost("/api/games/{gameDefinitionId}/matches", (string gameDefinitionId, ActiveGameMatchRegistry registry) =>
{
    var match = registry.Create(gameDefinitionId);
    return Results.Created($"/api/games/{gameDefinitionId}/matches/{match.MatchId}", ToResponse(match));
});

app.MapGet("/api/games/{gameDefinitionId}/matches/{matchId:guid}", (string gameDefinitionId, Guid matchId, ActiveGameMatchRegistry registry) =>
{
    if (!registry.TryGet(matchId, out var match) || match is null || !MatchesGame(match, gameDefinitionId))
    {
        return Results.NotFound(new ErrorResponse($"Match '{matchId}' was not found for game '{gameDefinitionId}'."));
    }

    return Results.Ok(ToResponse(match));
});

app.MapPost("/api/games/{gameDefinitionId}/matches/{matchId:guid}/commands", (string gameDefinitionId, Guid matchId, CommandRequest request, ActiveGameMatchRegistry registry) =>
{
    try
    {
        if (!registry.TryGet(matchId, out var match) || match is null || !MatchesGame(match, gameDefinitionId))
        {
            return Results.NotFound(new ErrorResponse($"Match '{matchId}' was not found for game '{gameDefinitionId}'."));
        }

        var command = new GameCommand(request.CommandName, request.Arguments);
        var updatedMatch = registry.Execute(matchId, command);
        return Results.Ok(ToResponse(updatedMatch));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new ErrorResponse(exception.Message));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ErrorResponse(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ErrorResponse(exception.Message));
    }
});

app.Run();

return;

static bool MatchesGame(ActiveGameMatch match, string gameDefinitionId) =>
    string.Equals(match.Match.State.GameDefinitionId, gameDefinitionId, StringComparison.OrdinalIgnoreCase);

static MatchResponse ToResponse(ActiveGameMatch activeMatch) =>
    new(
        activeMatch.MatchId,
        activeMatch.Match.State.GameDefinitionId,
        activeMatch.LastMessage,
        activeMatch.Match.State);

internal sealed record ApiRootResponse(
    string Service,
    IReadOnlyList<GameDefinitionResponse> Games);

internal sealed record GameDefinitionResponse(
    string Id,
    string DisplayName);

internal sealed record CommandRequest(
    string CommandName,
    Dictionary<string, string>? Arguments = null);

internal sealed record MatchResponse(
    Guid MatchId,
    string GameDefinitionId,
    string LastMessage,
    object State);

internal sealed record ErrorResponse(string Message);
