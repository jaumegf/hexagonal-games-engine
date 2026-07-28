using HexStrategy.Core.Contracts;

namespace HexStrategy.Application.Games;

public sealed record GameMatch(
    Guid MatchId,
    IGameDefinition Definition,
    IGameState State);
