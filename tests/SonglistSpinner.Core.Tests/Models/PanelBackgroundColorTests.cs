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

    [Theory]
    [InlineData("")]
    [InlineData("black")]
    [InlineData("#12345")]
    [InlineData("rgba(0,0,0,1)")]
    public void Given_ValueThatIsNotAHexColour_When_Constructed_Then_RejectsIt(string hex)
    {
        var exception = Assert.Throws<ArgumentException>(() => new PanelBackgroundColor(hex, 1.0));

        Assert.Equal("hex", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(double.NaN, PanelBackgroundColor.DefaultOpacity)]
    public void Given_OpacityOutsideZeroToOne_When_Constructed_Then_ClampsIt(double opacity, double expected)
    {
        var color = new PanelBackgroundColor("#abcdef", opacity);

        Assert.Equal("#ABCDEF", color.Hex);
        Assert.Equal(expected, color.Opacity);
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
