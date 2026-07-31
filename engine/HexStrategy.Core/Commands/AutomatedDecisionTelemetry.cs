using HexStrategy.Core.Players;

namespace HexStrategy.Core.Commands;

public sealed record AutomatedDecisionTelemetry(
    string PlayerId,
    string PlayerDisplayName,
    PlayerControllerType ControllerType,
    int SearchDepth,
    int TimeBudgetMilliseconds,
    double SecondChoiceProbability,
    int MaxCandidateCount,
    int LegalCommandCount,
    int CandidateCommandCount,
    int NodesVisited,
    int LeafEvaluations,
    int ChosenCommandScore,
    double ElapsedMilliseconds,
    bool TimeBudgetReached,
    string ChosenCommandDescription,
    string DecisionRuleCode,
    string DecisionRuleName);
