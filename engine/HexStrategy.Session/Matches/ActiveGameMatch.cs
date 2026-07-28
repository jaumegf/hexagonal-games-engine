using HexStrategy.Application.Games;

namespace HexStrategy.Session.Matches;

public sealed record ActiveGameMatch(
    Guid MatchId,
    GameMatch Match,
    string LastMessage);
