using HexStrategy.Application.Games;
using HexStrategy.Core.Contracts;
using HexStrategy.Game.KingOfTheHill;
using HexStrategy.Session.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameDefinition, KingOfTheHillGameDefinition>();
builder.Services.AddSingleton<GameCatalog>();
builder.Services.AddSingleton<GameMatchService>();
builder.Services.AddSingleton<GameSessionRegistry>();

var app = builder.Build();

var gameCatalog = app.Services.GetRequiredService<GameCatalog>();
var matchService = app.Services.GetRequiredService<GameMatchService>();
var sampleMatch = matchService.StartNew(KingOfTheHillGameDefinition.GameDefinitionId);

app.MapGet("/", () => Results.Ok(new
{
    Service = "HexStrategy.Host.Web",
    Status = "Ready",
    RegisteredGames = gameCatalog
        .Definitions
        .Select(definition => definition.Metadata.DisplayName),
    SampleMatch = new
    {
        sampleMatch.MatchId,
        sampleMatch.State.CurrentPlayerId,
        sampleMatch.State.TurnNumber
    }
}));

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.Run();
