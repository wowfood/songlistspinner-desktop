using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public class PanelBackgroundColorTests
{
    [Fact]
    public void Given_RgbaColor_When_Parsed_Then_ExtractsHexAndOpacity()
    {
        var color = PanelBackgroundColor.Parse("rgba(12, 34, 56, 0.7)");

        Assert.Equal("#0C2238", color.Hex);
        Assert.Equal(0.7, color.Opacity);
    }

    [Fact]
    public void Given_HexColor_When_Parsed_Then_DefaultsToOpaque()
    {
        var color = PanelBackgroundColor.Parse("#abcdef");

        Assert.Equal("#ABCDEF", color.Hex);
        Assert.Equal(1.0, color.Opacity);
    }

    [Fact]
    public void Given_NoOverride_When_Resolved_Then_PreservesInheritedBackground()
    {
        const string inherited = "rgba(12, 34, 56, 0.7)";

        var resolved = PanelBackgroundColor.Resolve(inherited, null);

        Assert.Equal(inherited, resolved);
    }

    [Theory]
    [InlineData(-0.5, "rgba(12,34,56,0.00)")]
    [InlineData(0.42, "rgba(12,34,56,0.42)")]
    [InlineData(1.5, "rgba(12,34,56,1.00)")]
    public void Given_Override_When_Resolved_Then_ReplacesAndClampsOpacity(
        double opacity,
        string expected)
    {
        var resolved = PanelBackgroundColor.Resolve("rgba(12, 34, 56, 0.7)", opacity);

        Assert.Equal(expected, resolved);
    }
}
