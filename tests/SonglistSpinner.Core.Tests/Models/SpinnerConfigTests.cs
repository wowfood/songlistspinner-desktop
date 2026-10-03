using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Models;

public class SpinnerConfigTests
{
    [Fact]
    public void Given_DefaultConfig_When_ItsWheelColorIsChanged_Then_NewConfigsKeepTheDefaultColors()
    {
        var changed = new SpinnerConfig();

        changed.WheelColors[0] = "#000000";

        Assert.Equal(
            ["#ff6b6b", "#4ecdc4", "#45b7d1", "#f9ca24", "#6c5ce7", "#a29bfe", "#fd79a8", "#fdcb6e"],
            new SpinnerConfig().WheelColors);
    }
}
