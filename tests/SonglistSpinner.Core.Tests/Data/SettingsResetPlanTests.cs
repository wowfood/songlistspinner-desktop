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
                (nameof(SettingsDto.WheelColors), "Wheel palette", "Wheel colors"),
                (field.PropertyName, field.Section, field.Label)),
            field => Assert.Equal(
                (nameof(SettingsDto.DebugMode), "Advanced", "Diagnostic output"),
                (field.PropertyName, field.Section, field.Label)));
    }

    [Fact]
    public void Given_WheelPaletteScope_When_GetAffectedFields_Then_OnlyReturnsWheelPaletteFields()
    {
        var current = new SettingsDto
        {
            WheelColors = "[]",
            BackgroundColor = "#abcdef",
            ColorText = "#123456"
        };

        var affectedFields = SettingsResetPlan.GetAffectedFields(
            current,
            new SettingsDto(),
            SettingsResetScope.WheelPalette);

        var field = Assert.Single(affectedFields);
        Assert.Equal(nameof(SettingsDto.WheelColors), field.PropertyName);
        Assert.Equal("Wheel palette", field.Section);
    }

    [Fact]
    public void Given_WheelPaletteScope_When_ApplyDefaults_Then_UnrelatedFieldsRemainUnchanged()
    {
        var defaults = new SettingsDto();
        var current = new SettingsDto
        {
            WheelColors = "[]",
            BackgroundColor = "#abcdef",
            ColorText = "#123456"
        };

        SettingsResetPlan.ApplyDefaults(current, defaults, SettingsResetScope.WheelPalette);

        Assert.Equal(defaults.WheelColors, current.WheelColors);
        Assert.Equal("#abcdef", current.BackgroundColor);
        Assert.Equal("#123456", current.ColorText);
    }

    [Theory]
    [InlineData(SettingsResetScope.All)]
    [InlineData(SettingsResetScope.PlayedSongsPanel)]
    [InlineData(SettingsResetScope.NowPlayingPanel)]
    [InlineData(SettingsResetScope.WinnerDialog)]
    [InlineData(SettingsResetScope.Background)]
    [InlineData(SettingsResetScope.WheelPalette)]
    [InlineData(SettingsResetScope.OverlayColors)]
    public void Given_ResetScope_When_ApplyDefaults_Then_OnlyAffectedFieldsAreChanged(
        SettingsResetScope scope)
    {
        var defaults = new SettingsDto();
        var current = CreateSettingsDifferentFrom(defaults);
        var properties = typeof(SettingsDto).GetProperties().Where(property => property.CanRead && property.CanWrite);
        var originalValues = properties.ToDictionary(property => property.Name, property => property.GetValue(current));
        var affectedProperties = SettingsResetPlan.GetAffectedFields(current, defaults, scope)
            .Select(field => field.PropertyName)
            .ToHashSet(StringComparer.Ordinal);

        SettingsResetPlan.ApplyDefaults(current, defaults, scope);

        Assert.NotEmpty(affectedProperties);
        foreach (var property in properties)
        {
            var expected = affectedProperties.Contains(property.Name)
                ? property.GetValue(defaults)
                : originalValues[property.Name];
            Assert.Equal(expected, property.GetValue(current));
        }
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

    private static SettingsDto CreateSettingsDifferentFrom(SettingsDto defaults)
    {
        var settings = new SettingsDto();
        foreach (var property in typeof(SettingsDto).GetProperties().Where(property => property.CanWrite))
        {
            var defaultValue = property.GetValue(defaults);
            object? differentValue = property.PropertyType switch
            {
                { } type when type == typeof(string) => $"custom-{property.Name}",
                { } type when type == typeof(bool) => !(bool)defaultValue!,
                { } type when type == typeof(int) => (int)defaultValue! + 1,
                { } type when type == typeof(double?) => defaultValue is null ? 0.42 : null,
                _ => throw new InvalidOperationException($"Unsupported settings type: {property.PropertyType}")
            };
            property.SetValue(settings, differentValue);
        }

        return settings;
    }
}
