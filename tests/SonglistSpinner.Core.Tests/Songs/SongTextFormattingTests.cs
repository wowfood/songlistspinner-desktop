using SonglistSpinner.Core.Songs;
using Xunit;

namespace SonglistSpinner.Core.Tests.Songs;

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
