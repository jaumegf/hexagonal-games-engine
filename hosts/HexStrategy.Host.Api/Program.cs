using System.Text.Json.Serialization;
using HexStrategy.Application.Games;
using HexStrategy.Core.Commands;
using HexStrategy.Core.Contracts;
using HexStrategy.Core.Players;
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
    Service = "HexStrategy.Host.Api",
    Message = "Use /api for the backend endpoints."
}));

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.MapGet("/api", (GameCatalog catalog) => Results.Ok(new ApiRootResponse(
    "HexStrategy.Host.Api",
    catalog.Definitions
        .OrderBy(definition => definition.Metadata.DisplayName)
        .Select(definition => new GameDefinitionResponse(
            definition.Metadata.Id,
            definition.Metadata.DisplayName))
        .ToArray())));

app.MapPost("/api/games/{gameDefinitionId}/matches", (string gameDefinitionId, CreateMatchRequest? request, ActiveGameMatchRegistry registry) =>
{
    var players = CreatePlayers(request);
    var match = registry.Create(gameDefinitionId, players);
    return Results.Created($"/api/games/{gameDefinitionId}/matches/{match.MatchId}", ToResponse(match));
});

app.MapPost("/api/games/{gameDefinitionId}/matches/import", (string gameDefinitionId, ImportMatchRequest request, ActiveGameMatchRegistry registry) =>
{
    try
    {
        if (!string.Equals(gameDefinitionId, KingOfTheHillGameDefinition.GameDefinitionId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ErrorResponse($"Snapshot import is not supported for game '{gameDefinitionId}'."));
        }

        if (request.State is null)
        {
            return Results.BadRequest(new ErrorResponse("Snapshot state is required."));
        }

        var restoredState = RestoreKingOfTheHillState(request.State);
        var match = registry.Import(gameDefinitionId, restoredState, request.LastMessage);
        return Results.Created($"/api/games/{gameDefinitionId}/matches/{match.MatchId}", ToResponse(match));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new ErrorResponse(exception.Message));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ErrorResponse(exception.Message));
    }
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

app.MapPost("/api/games/{gameDefinitionId}/matches/{matchId:guid}/automated-turn", (string gameDefinitionId, Guid matchId, ActiveGameMatchRegistry registry) =>
{
    try
    {
        if (!registry.TryGet(matchId, out var match) || match is null || !MatchesGame(match, gameDefinitionId))
        {
            return Results.NotFound(new ErrorResponse($"Match '{matchId}' was not found for game '{gameDefinitionId}'.")); 
        }

        var updatedMatch = registry.ExecuteAutomatedTurn(matchId);
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

static IReadOnlyList<PlayerToken> CreatePlayers(CreateMatchRequest? request)
{
    var player1Controller = ParseControllerType(request?.Player1Controller, "Human");
    var player2Controller = ParseControllerType(request?.Player2Controller, "IA4");

    return new[]
    {
        new PlayerToken("P1", "Player 1", player1Controller),
        new PlayerToken("P2", "Player 2", player2Controller)
    };
}

static PlayerControllerType ParseControllerType(string? value, string fallback)
{
    var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    return normalized.ToUpperInvariant() switch
    {
        "HUMAN" => PlayerControllerType.Human,
        "IA1" => PlayerControllerType.IaLevel4,
        "IALEVEL1" => PlayerControllerType.IaLevel4,
        "IA2" => PlayerControllerType.IaLevel4,
        "IALEVEL2" => PlayerControllerType.IaLevel4,
        "IA3" => PlayerControllerType.IaLevel4,
        "IALEVEL3" => PlayerControllerType.IaLevel4,
        "IA4" => PlayerControllerType.IaLevel4,
        "IALEVEL4" => PlayerControllerType.IaLevel4,
        _ => throw new ArgumentException($"Unsupported player controller '{normalized}'.")
    };
}

static KingOfTheHillGameState RestoreKingOfTheHillState(KingOfTheHillStateSnapshot state)
{
    ArgumentNullException.ThrowIfNull(state);

    var boardCoordinates = state.Board?.Coordinates?
        .Select(coordinate => new HexStrategy.Core.Hexes.HexCoordinate(coordinate.Q, coordinate.R))
        .ToArray()
        ?? throw new ArgumentException("Snapshot board coordinates are required.");

    var players = state.Players?
        .Select(player => new PlayerToken(
            player.Id,
            player.DisplayName,
            ParseControllerType(player.ControllerType, "Human")))
        .ToArray()
        ?? throw new ArgumentException("Snapshot players are required.");

    var units = state.Units?
        .Select(unit => new KingOfTheHillUnitState(
            unit.Id,
            unit.OwnerPlayerId,
            new HexStrategy.Core.Hexes.HexCoordinate(unit.Position.Q, unit.Position.R),
            unit.MemberUnitIds?.ToArray() ?? throw new ArgumentException($"Snapshot unit '{unit.Id}' members are required.")))
        .ToArray()
        ?? throw new ArgumentException("Snapshot units are required.");

    var scores = state.ControlScores is null
        ? throw new ArgumentException("Snapshot control scores are required.")
        : new Dictionary<string, int>(state.ControlScores, StringComparer.OrdinalIgnoreCase);

    var retiredDefenderIds = state.RetiredDefenderIds?.ToArray()
        ?? Array.Empty<string>();

    return new KingOfTheHillGameState(
        new HexStrategy.Core.Hexes.HexBoard(boardCoordinates),
        players,
        units,
        retiredDefenderIds,
        scores,
        state.CurrentPlayerId,
        state.TurnNumber,
        state.IsCompleted,
        state.WinnerPlayerId);
}

static bool MatchesGame(ActiveGameMatch match, string gameDefinitionId) =>
    string.Equals(match.Match.State.GameDefinitionId, gameDefinitionId, StringComparison.OrdinalIgnoreCase);

static MatchResponse ToResponse(ActiveGameMatch activeMatch) =>
    new(
        activeMatch.MatchId,
        activeMatch.Match.State.GameDefinitionId,
        activeMatch.LastMessage,
        activeMatch.Match.State,
        activeMatch.LastAutomatedDecisionTelemetry);

internal sealed record ApiRootResponse(
    string Service,
    IReadOnlyList<GameDefinitionResponse> Games);

internal sealed record GameDefinitionResponse(
    string Id,
    string DisplayName);

internal sealed record CreateMatchRequest(
    string? Player1Controller = null,
    string? Player2Controller = null);

internal sealed record ImportMatchRequest(
    KingOfTheHillStateSnapshot? State,
    string? LastMessage = null);

internal sealed record KingOfTheHillStateSnapshot(
    BoardSnapshot Board,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<UnitSnapshot> Units,
    IReadOnlyList<string>? RetiredDefenderIds,
    IReadOnlyDictionary<string, int> ControlScores,
    string CurrentPlayerId,
    int TurnNumber,
    bool IsCompleted,
    string? WinnerPlayerId);

internal sealed record BoardSnapshot(
    IReadOnlyList<CoordinateSnapshot> Coordinates);

internal sealed record CoordinateSnapshot(
    int Q,
    int R);

internal sealed record PlayerSnapshot(
    string Id,
    string DisplayName,
    string ControllerType);

internal sealed record UnitSnapshot(
    string Id,
    string OwnerPlayerId,
    CoordinateSnapshot Position,
    IReadOnlyList<string> MemberUnitIds);

internal sealed record CommandRequest(
    string CommandName,
    Dictionary<string, string>? Arguments = null);

internal sealed record MatchResponse(
    Guid MatchId,
    string GameDefinitionId,
    string LastMessage,
    object State,
    HexStrategy.Core.Commands.AutomatedDecisionTelemetry? LastAutomatedDecisionTelemetry);

internal sealed record ErrorResponse(string Message);
