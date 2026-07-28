namespace HexStrategy.Core.Commands;

public sealed record GameCommand(
    string Name,
    IReadOnlyDictionary<string, string>? Arguments = null)
{
    public string GetRequiredArgument(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Arguments is null || !Arguments.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required command argument '{key}'.");
        }

        return value;
    }
}
