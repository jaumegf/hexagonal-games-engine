using HexStrategy.Core.Players;

namespace HexStrategy.Core.Contracts;

public interface ITurnBasedGameState : IGameState
{
    IReadOnlyList<PlayerToken> Players { get; }

    new string CurrentPlayerId { get; }

    new bool IsCompleted { get; }

    PlayerToken CurrentPlayer { get; }
}
