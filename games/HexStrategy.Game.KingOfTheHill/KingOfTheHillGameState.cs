using HexStrategy.Core.Contracts;
using HexStrategy.Core.Hexes;
using HexStrategy.Core.Players;

namespace HexStrategy.Game.KingOfTheHill;

public sealed record KingOfTheHillGameState(
    HexBoard Board,
    IReadOnlyList<PlayerToken> Players,
    IReadOnlyList<KingOfTheHillUnitState> Units,
    IReadOnlyCollection<string> RetiredDefenderIds,
    IReadOnlyDictionary<string, int> ControlScores,
    string CurrentPlayerId,
    int TurnNumber,
    bool IsCompleted,
    string? WinnerPlayerId) : ITurnBasedGameState
{
    public const int MaximumBlockStrength = 4;

    public string GameDefinitionId => KingOfTheHillGameDefinition.GameDefinitionId;

    public HexCoordinate ObjectiveCoordinate => HexCoordinate.Origin;

    public static KingOfTheHillGameState CreateDefault(IReadOnlyList<PlayerToken>? players = null)
    {
        var resolvedPlayers = players?.ToArray() ?? new[]
        {
            new PlayerToken("P1", "Player 1", PlayerControllerType.Human),
            new PlayerToken("P2", "Player 2", PlayerControllerType.Human)
        };

        if (resolvedPlayers.Length != 2)
        {
            throw new ArgumentException("King of the Hill requires exactly two players.", nameof(players));
        }

        var units = new[]
        {
            KingOfTheHillUnitState.CreateSingle("1B", "P1", new HexCoordinate(-1, 4)),
            KingOfTheHillUnitState.CreateSingle("1C", "P1", new HexCoordinate(0, 4)),
            KingOfTheHillUnitState.CreateSingle("1D", "P1", new HexCoordinate(1, 4)),
            KingOfTheHillUnitState.CreateSingle("1E", "P1", new HexCoordinate(-5, 2)),
            KingOfTheHillUnitState.CreateSeededBlock("1F", "P1", new HexCoordinate(-4, 2), 2),
            KingOfTheHillUnitState.CreateSingle("1G", "P1", new HexCoordinate(2, 2)),
            KingOfTheHillUnitState.CreateSingle("1H", "P1", new HexCoordinate(-5, 1)),
            KingOfTheHillUnitState.CreateSingle("1I", "P1", new HexCoordinate(3, 1)),
            KingOfTheHillUnitState.CreateSingle("1J", "P1", new HexCoordinate(-3, 4)),
            KingOfTheHillUnitState.CreateSingle("1K", "P1", new HexCoordinate(-2, 5)),
            KingOfTheHillUnitState.CreateSingle("1L", "P1", new HexCoordinate(-1, 5)),
            KingOfTheHillUnitState.CreateSingle("1M", "P1", new HexCoordinate(0, 5)),
            KingOfTheHillUnitState.CreateSingle("1N", "P1", new HexCoordinate(1, 5)),
            KingOfTheHillUnitState.CreateSingle("1O", "P1", new HexCoordinate(3, 4)),
            KingOfTheHillUnitState.CreateSingle("1P", "P1", new HexCoordinate(-2, 4)),
            KingOfTheHillUnitState.CreateSingle("1Q", "P1", new HexCoordinate(2, 4)),
            KingOfTheHillUnitState.CreateSingle("1R", "P1", new HexCoordinate(-5, 3)),
            KingOfTheHillUnitState.CreateSingle("1S", "P1", new HexCoordinate(3, 3)),
            KingOfTheHillUnitState.CreateSeededBlock("1T", "P1", new HexCoordinate(1, -2), 3),
            KingOfTheHillUnitState.CreateSingle("1U", "P1", new HexCoordinate(3, 2)),
            KingOfTheHillUnitState.CreateSeededBlock("1V", "P1", new HexCoordinate(-1, -1), 3),
            KingOfTheHillUnitState.CreateSingle("1W", "P1", new HexCoordinate(-6, 2)),
            KingOfTheHillUnitState.CreateSeededBlock("1X", "P1", new HexCoordinate(2, -1), 3),
            KingOfTheHillUnitState.CreateSingle("2B", "P2", new HexCoordinate(1, -4)),
            KingOfTheHillUnitState.CreateSingle("2C", "P2", new HexCoordinate(0, -4)),
            KingOfTheHillUnitState.CreateSingle("2D", "P2", new HexCoordinate(-1, -4)),
            KingOfTheHillUnitState.CreateSingle("2E", "P2", new HexCoordinate(5, -2)),
            KingOfTheHillUnitState.CreateSeededBlock("2F", "P2", new HexCoordinate(4, -2), 2),
            KingOfTheHillUnitState.CreateSingle("2G", "P2", new HexCoordinate(-2, -2)),
            KingOfTheHillUnitState.CreateSingle("2H", "P2", new HexCoordinate(5, -1)),
            KingOfTheHillUnitState.CreateSingle("2I", "P2", new HexCoordinate(-3, -1)),
            KingOfTheHillUnitState.CreateSingle("2J", "P2", new HexCoordinate(3, -4)),
            KingOfTheHillUnitState.CreateSingle("2K", "P2", new HexCoordinate(2, -5)),
            KingOfTheHillUnitState.CreateSingle("2L", "P2", new HexCoordinate(1, -5)),
            KingOfTheHillUnitState.CreateSingle("2M", "P2", new HexCoordinate(0, -5)),
            KingOfTheHillUnitState.CreateSingle("2N", "P2", new HexCoordinate(-1, -5)),
            KingOfTheHillUnitState.CreateSingle("2O", "P2", new HexCoordinate(-3, -4)),
            KingOfTheHillUnitState.CreateSingle("2P", "P2", new HexCoordinate(2, -4)),
            KingOfTheHillUnitState.CreateSingle("2Q", "P2", new HexCoordinate(-2, -4)),
            KingOfTheHillUnitState.CreateSingle("2R", "P2", new HexCoordinate(5, -3)),
            KingOfTheHillUnitState.CreateSingle("2S", "P2", new HexCoordinate(-2, -3)),
            KingOfTheHillUnitState.CreateSeededBlock("2T", "P2", new HexCoordinate(-1, 2), 3),
            KingOfTheHillUnitState.CreateSingle("2U", "P2", new HexCoordinate(-3, -2)),
            KingOfTheHillUnitState.CreateSeededBlock("2V", "P2", new HexCoordinate(1, 1), 3),
            KingOfTheHillUnitState.CreateSingle("2W", "P2", new HexCoordinate(6, -2)),
            KingOfTheHillUnitState.CreateSeededBlock("2X", "P2", new HexCoordinate(-2, 1), 3)
        };

        return new KingOfTheHillGameState(
            new HexBoard(CreateBoardCoordinates()),
            resolvedPlayers,
            units,
            Array.Empty<string>(),
            new Dictionary<string, int>
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            CurrentPlayerId: "P1",
            TurnNumber: 1,
            IsCompleted: false,
            WinnerPlayerId: null);
    }

    public PlayerToken CurrentPlayer => Players.Single(player => player.Id == CurrentPlayerId);

    public KingOfTheHillUnitState? FindUnit(string unitId) =>
        Units.SingleOrDefault(unit => string.Equals(unit.Id, unitId, StringComparison.OrdinalIgnoreCase));

    public KingOfTheHillUnitState? FindUnitAt(HexCoordinate coordinate) =>
        Units.SingleOrDefault(unit => unit.Position == coordinate);

    public bool IsOccupied(HexCoordinate coordinate) =>
        FindUnitAt(coordinate) is not null;

    public bool IsDefenderRetired(string unitId) =>
        RetiredDefenderIds.Any(existingId => string.Equals(existingId, unitId, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<HexCoordinate> CreateBoardCoordinates() =>
        CreateCoordinatesFromRows(
            (-5, -1, 4),
            (-4, -3, 7),
            (-3, -2, 6),
            (-2, -1, 5),
            (-1, -2, 7),
            (0, -4, 9),
            (1, -4, 7),
            (2, -3, 5),
            (3, -2, 6),
            (4, -3, 7),
            (5, -2, 4))
        .Concat(
            [
                new HexCoordinate(-3, -1),
                new HexCoordinate(-2, -2),
                new HexCoordinate(-3, -2),
                new HexCoordinate(-4, 2),
                new HexCoordinate(-5, 1),
                new HexCoordinate(-5, 2),
                new HexCoordinate(-5, 3),
                new HexCoordinate(-6, 2),
                new HexCoordinate(3, 1),
                new HexCoordinate(2, 2),
                new HexCoordinate(3, 2),
                new HexCoordinate(4, -3),
                new HexCoordinate(4, -2),
                new HexCoordinate(5, -3),
                new HexCoordinate(5, -2),
                new HexCoordinate(5, -1),
                new HexCoordinate(6, -2)
            ])
        .ToArray();

    private static IReadOnlyList<HexCoordinate> CreateCoordinatesFromRows(
        params (int r, int startQ, int count)[] rows) =>
        rows
            .SelectMany(row => Enumerable
                .Range(row.startQ, row.count)
                .Select(q => new HexCoordinate(q, row.r)))
            .ToArray();
}
