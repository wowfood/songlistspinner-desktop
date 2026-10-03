using SonglistSpinner.Core.Settings;
using Xunit;

namespace SonglistSpinner.Core.Tests.Settings;

public class CssNamedColorsTests
{
    [Theory]
    [InlineData("wheat", "#f5deb3")]
    [InlineData("WHEAT", "#f5deb3")]
    [InlineData("grey", "#808080")]
    [InlineData("gray", "#808080")]
    public void Given_KnownColorName_When_ConvertedToHex_Then_ReturnsItsHexValue(string color, string expected)
    {
        var hex = CssNamedColors.ToHex(color, "#000000");

        Assert.Equal(expected, hex);
    }

    [Fact]
    public void Given_HexValue_When_ConvertedToHex_Then_ReturnsItUnchanged()
    {
        var hex = CssNamedColors.ToHex("#ABCDEF", "#000000");

        Assert.Equal("#ABCDEF", hex);
    }

    [Fact]
    public void Given_UnrecognisedValue_When_ConvertedToHex_Then_ReturnsTheCallersFallback()
    {
        var hex = CssNamedColors.ToHex("rgba(0, 0, 0, 0.7)", "#f5deb3");

        Assert.Equal("#f5deb3", hex);
    }
}
