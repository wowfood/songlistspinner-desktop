using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public sealed class SongTextFormattingTests
{
    [Fact]
    public void Given_SeparatorPresets_When_Read_Then_ValuesRemainStableAndUnique()
    {
        Assert.Equal(
            [
                " | ",
                " • ",
                " · ",
                " ◆ ",
                " ★ ",
                " / ",
                " — ",
                " → "
            ],
            SongTextFormatting.Presets);
        Assert.Equal(
            SongTextFormatting.Presets.Count,
            SongTextFormatting.Presets.Distinct(StringComparer.Ordinal).Count());
    }
}
