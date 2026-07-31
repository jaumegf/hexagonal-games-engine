namespace HexStrategy.Core.Commands;

public sealed record AutomatedDecisionResult(
    GameCommand Command,
    AutomatedDecisionTelemetry Telemetry);
