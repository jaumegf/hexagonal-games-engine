using HexStrategy.Core.Commands;
using HexStrategy.Core.Players;

namespace HexStrategy.Core.Contracts;

public interface IAiCapableGameDefinition
{
    AutomatedDecisionResult ChooseAutomatedCommand(IGameState state, PlayerToken player);
}
