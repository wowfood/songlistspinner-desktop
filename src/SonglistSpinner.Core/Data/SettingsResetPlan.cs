namespace SonglistSpinner.Core.Data;

public sealed record SettingsResetField(string PropertyName, string Section, string Label);

public static class SettingsResetPlan
{
    private const string ConnectionSection = "Connection";
    private const string SpinnerSection = "Spinner & Queue";
    private const string OverlaySection = "Overlay Layout";
    private const string AppearanceSection = "Appearance";
    private const string AdvancedSection = "Advanced";

    private static readonly FieldDefinition[] Definitions =
    [
        Define(nameof(SettingsDto.DefaultStreamerName), ConnectionSection, "Default StreamerSongList name",
            settings => settings.DefaultStreamerName),
        Define(nameof(SettingsDto.StreamerPlatform), ConnectionSection, "Platform identity",
            settings => settings.StreamerPlatform),
        Define(nameof(SettingsDto.HideChangeOptionWhenDefault), ConnectionSection,
            "Change Streamer visibility", settings => settings.HideChangeOptionWhenDefault),

        Define(nameof(SettingsDto.UpdateQueueAfterSpin), SpinnerSection, "Preferred winner action",
            settings => settings.UpdateQueueAfterSpin),
        Define(nameof(SettingsDto.DisplayNowPlaying), SpinnerSection, "Now Playing workflow",
            settings => settings.DisplayNowPlaying),
        Define(nameof(SettingsDto.ExcludePlayedSongs), SpinnerSection, "Played-song exclusion",
            settings => settings.ExcludePlayedSongs),
        Define(nameof(SettingsDto.PlayHistoryPeriod), SpinnerSection, "Play history period",
            settings => settings.PlayHistoryPeriod),

        Define(nameof(SettingsDto.SongListFields), OverlaySection, "Played Songs display fields",
            settings => settings.SongListFields),
        Define(nameof(SettingsDto.PlayedListShowNumbers), OverlaySection, "Played Songs sequence numbers",
            settings => settings.PlayedListShowNumbers),
        Define(nameof(SettingsDto.PlayedListNumberingStart), OverlaySection, "Played Songs numbering direction",
            settings => settings.PlayedListNumberingStart),
        Define(nameof(SettingsDto.PlayedListPosition), OverlaySection, "Played Songs panel position",
            settings => settings.PlayedListPosition),
        Define(nameof(SettingsDto.PlayedListFontFamily), OverlaySection, "Played Songs font",
            settings => settings.PlayedListFontFamily),
        Define(nameof(SettingsDto.PlayedListFontSize), OverlaySection, "Played Songs font size",
            settings => settings.PlayedListFontSize),
        Define(nameof(SettingsDto.PlayedListMaxLines), OverlaySection, "Played Songs maximum lines",
            settings => settings.PlayedListMaxLines),
        Define(nameof(SettingsDto.NowPlayingFields), OverlaySection, "Now Playing display fields",
            settings => settings.NowPlayingFields),
        Define(nameof(SettingsDto.NowPlayingPosition), OverlaySection, "Now Playing panel position",
            settings => settings.NowPlayingPosition),
        Define(nameof(SettingsDto.NowPlayingFontFamily), OverlaySection, "Now Playing font",
            settings => settings.NowPlayingFontFamily),
        Define(nameof(SettingsDto.NowPlayingFontSize), OverlaySection, "Now Playing font size",
            settings => settings.NowPlayingFontSize),
        Define(nameof(SettingsDto.NowPlayingWidth), OverlaySection, "Now Playing width",
            settings => settings.NowPlayingWidth),
        Define(nameof(SettingsDto.NowPlayingBackgroundOpacity), OverlaySection, "Now Playing opacity",
            settings => settings.NowPlayingBackgroundOpacity),
        Define(nameof(SettingsDto.WinnerDialogFields), OverlaySection, "Winner dialog display fields",
            settings => settings.WinnerDialogFields),
        Define(nameof(SettingsDto.WinnerDialogFontFamily), OverlaySection, "Winner dialog font",
            settings => settings.WinnerDialogFontFamily),
        Define(nameof(SettingsDto.WinnerDialogFontSize), OverlaySection, "Winner dialog font size",
            settings => settings.WinnerDialogFontSize),
        Define(nameof(SettingsDto.WinnerDialogWidth), OverlaySection, "Winner dialog width",
            settings => settings.WinnerDialogWidth),
        Define(nameof(SettingsDto.WinnerDialogShowQueuePosition), OverlaySection, "Winner queue position",
            settings => settings.WinnerDialogShowQueuePosition),

        Define(nameof(SettingsDto.WheelColors), AppearanceSection, "Wheel colors",
            settings => settings.WheelColors),
        Define(nameof(SettingsDto.BackgroundMode), AppearanceSection, "Background mode",
            settings => settings.BackgroundMode),
        Define(nameof(SettingsDto.BackgroundColor), AppearanceSection, "Background color",
            settings => settings.BackgroundColor),
        Define(nameof(SettingsDto.BackgroundImage), AppearanceSection, "Background image",
            settings => settings.BackgroundImage),
        Define(nameof(SettingsDto.ColorText), AppearanceSection, "Text color",
            settings => settings.ColorText),
        Define(nameof(SettingsDto.ColorStatusBackground), AppearanceSection, "Status background color",
            settings => settings.ColorStatusBackground),
        Define(nameof(SettingsDto.ColorPlayedListBackground), AppearanceSection, "Panel background color",
            settings => settings.ColorPlayedListBackground),
        Define(nameof(SettingsDto.ColorPlayedItemBackground), AppearanceSection, "Played-song background color",
            settings => settings.ColorPlayedItemBackground),
        Define(nameof(SettingsDto.ColorResizeHandleBackground), AppearanceSection, "Resize handle color",
            settings => settings.ColorResizeHandleBackground),
        Define(nameof(SettingsDto.ColorResizeHandleHoverBackground), AppearanceSection, "Resize handle hover color",
            settings => settings.ColorResizeHandleHoverBackground),
        Define(nameof(SettingsDto.ColorToggleBackground), AppearanceSection, "Toggle background color",
            settings => settings.ColorToggleBackground),
        Define(nameof(SettingsDto.ColorButtonBackground), AppearanceSection, "Button background color",
            settings => settings.ColorButtonBackground),
        Define(nameof(SettingsDto.ColorButtonText), AppearanceSection, "Button text color",
            settings => settings.ColorButtonText),
        Define(nameof(SettingsDto.ColorPointer), AppearanceSection, "Wheel pointer color",
            settings => settings.ColorPointer),

        Define(nameof(SettingsDto.DebugMode), AdvancedSection, "Diagnostic output",
            settings => settings.DebugMode)
    ];

    internal static IReadOnlyList<string> SupportedPropertyNames { get; } =
        Array.AsReadOnly(Definitions.Select(definition => definition.PropertyName).ToArray());

    public static IReadOnlyList<SettingsResetField> GetAffectedFields(
        SettingsDto current,
        SettingsDto defaults)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(defaults);

        return Definitions
            .Where(definition => !Equals(definition.GetValue(current), definition.GetValue(defaults)))
            .Select(definition => new SettingsResetField(
                definition.PropertyName,
                definition.Section,
                definition.Label))
            .ToArray();
    }

    private static FieldDefinition Define(
        string propertyName,
        string section,
        string label,
        Func<SettingsDto, object?> getValue)
    {
        return new FieldDefinition(propertyName, section, label, getValue);
    }

    private sealed record FieldDefinition(
        string PropertyName,
        string Section,
        string Label,
        Func<SettingsDto, object?> GetValue);
}
