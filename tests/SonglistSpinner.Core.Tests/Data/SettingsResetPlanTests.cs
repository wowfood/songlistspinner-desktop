using SonglistSpinner.Core.Data;
using Xunit;

namespace SonglistSpinner.Core.Tests.Data;

public class SettingsResetPlanTests
{
    [Fact]
    public void Given_SettingsMatchDefaults_When_GetAffectedFields_Then_ReturnsNoFields()
    {
        var defaults = new SettingsDto();

        var affectedFields = SettingsResetPlan.GetAffectedFields(defaults, new SettingsDto());

        Assert.Empty(affectedFields);
    }

    [Fact]
    public void Given_SettingsDifferFromDefaults_When_GetAffectedFields_Then_ReturnsGroupedFriendlyFields()
    {
        var current = new SettingsDto
        {
            DefaultStreamerName = "wowfood",
            ExcludePlayedSongs = true,
            WheelColors = "[]",
            DebugMode = true
        };

        var affectedFields = SettingsResetPlan.GetAffectedFields(current, new SettingsDto());

        Assert.Collection(
            affectedFields,
            field => Assert.Equal(
                (nameof(SettingsDto.DefaultStreamerName), "Connection", "Default StreamerSongList name"),
                (field.PropertyName, field.Section, field.Label)),
            field => Assert.Equal(
                (nameof(SettingsDto.ExcludePlayedSongs), "Spinner & Queue", "Played-song exclusion"),
                (field.PropertyName, field.Section, field.Label)),
            field => Assert.Equal(
                (nameof(SettingsDto.WheelColors), "Appearance", "Wheel colors"),
                (field.PropertyName, field.Section, field.Label)),
            field => Assert.Equal(
                (nameof(SettingsDto.DebugMode), "Advanced", "Diagnostic output"),
                (field.PropertyName, field.Section, field.Label)));
    }

    [Fact]
    public void Given_SettingsDtoProperties_When_ReadingResetCatalog_Then_EveryPersistedFieldIsCoveredOnce()
    {
        var persistedProperties = typeof(SettingsDto)
            .GetProperties()
            .Where(property => property.CanRead && property.CanWrite)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var resetProperties = SettingsResetPlan.SupportedPropertyNames
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(persistedProperties, resetProperties);
        Assert.Equal(resetProperties.Length, resetProperties.Distinct(StringComparer.Ordinal).Count());
    }
}
