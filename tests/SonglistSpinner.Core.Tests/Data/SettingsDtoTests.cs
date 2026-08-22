using System.Text.Json;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using Xunit;

namespace SonglistSpinner.Core.Tests.Data;

public class SettingsDtoTests
{
    [Fact]
    public void Given_LegacySettingsWithoutNumbering_When_Deserialized_Then_UsesSafeDefaults()
    {
        var settings = JsonSerializer.Deserialize<SettingsDto>("{}");

        Assert.NotNull(settings);
        Assert.False(settings.PlayedListShowNumbers);
        Assert.Equal(SpinnerPlayedListConfig.NumberingStartBottom, settings.PlayedListNumberingStart);
    }
}
