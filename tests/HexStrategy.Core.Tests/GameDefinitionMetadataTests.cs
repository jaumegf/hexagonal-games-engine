using HexStrategy.Core.Contracts;

namespace HexStrategy.Core.Tests;

public sealed class GameDefinitionMetadataTests
{
    [Fact]
    public void Constructor_RejectsBlankId()
    {
        var action = () => new GameDefinitionMetadata("", "Sample");

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_CapturesSuppliedValues()
    {
        var metadata = new GameDefinitionMetadata("sample", "Sample");

        Assert.Equal("sample", metadata.Id);
        Assert.Equal("Sample", metadata.DisplayName);
    }
}
