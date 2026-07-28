namespace HexStrategy.Core.Contracts;

public sealed record GameDefinitionMetadata
{
    public GameDefinitionMetadata(string id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Game definition id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Game definition display name cannot be empty.", nameof(displayName));
        }

        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }

    public string DisplayName { get; }
}
