namespace SonglistSpinner.Core.Data;

public enum SettingsResetScope
{
    All,
    PlayedSongsPanel,
    NowPlayingPanel,
    WinnerDialog,
    Background,
    WheelPalette,
    OverlayColors
}

public sealed record SettingsResetField(string PropertyName, string Section, string Label);

public static class SettingsResetPlan
{
    private const string ConnectionSection = "Connection";
    private const string SpinnerSection = "Spinner & Queue";
    private const string PlayedSongsSection = "Played Songs panel";
    private const string NowPlayingSection = "Now Playing panel";
    private const string WinnerDialogSection = "Winner dialog";
    private const string BackgroundSection = "Background";
    private const string WheelPaletteSection = "Wheel palette";
    private const string OverlayColorsSection = "Overlay colors";
    private const string AdvancedSection = "Advanced";

    private static readonly FieldDefinition[] Definitions =
    [
        Define(nameof(SettingsDto.DefaultStreamerName), ConnectionSection, "Default StreamerSongList name", null,
            settings => settings.DefaultStreamerName,
            (settings, value) => settings.DefaultStreamerName = value),
        Define(nameof(SettingsDto.StreamerPlatform), ConnectionSection, "Platform identity", null,
            settings => settings.StreamerPlatform,
            (settings, value) => settings.StreamerPlatform = value),
        Define(nameof(SettingsDto.HideChangeOptionWhenDefault), ConnectionSection,
            "Change Streamer visibility", null,
            settings => settings.HideChangeOptionWhenDefault,
            (settings, value) => settings.HideChangeOptionWhenDefault = value),

        Define(nameof(SettingsDto.UpdateQueueAfterSpin), SpinnerSection, "Preferred winner action", null,
            settings => settings.UpdateQueueAfterSpin,
            (settings, value) => settings.UpdateQueueAfterSpin = value),
        Define(nameof(SettingsDto.DisplayNowPlaying), SpinnerSection, "Now Playing workflow", null,
            settings => settings.DisplayNowPlaying,
            (settings, value) => settings.DisplayNowPlaying = value),
        Define(nameof(SettingsDto.ExcludePlayedSongs), SpinnerSection, "Played-song exclusion", null,
            settings => settings.ExcludePlayedSongs,
            (settings, value) => settings.ExcludePlayedSongs = value),
        Define(nameof(SettingsDto.PlayHistoryPeriod), SpinnerSection, "Play history period", null,
            settings => settings.PlayHistoryPeriod,
            (settings, value) => settings.PlayHistoryPeriod = value),

        Define(nameof(SettingsDto.SongListFields), PlayedSongsSection, "Display fields",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.SongListFields,
            (settings, value) => settings.SongListFields = value),
        Define(nameof(SettingsDto.PlayedListShowNumbers), PlayedSongsSection, "Sequence numbers",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.PlayedListShowNumbers,
            (settings, value) => settings.PlayedListShowNumbers = value),
        Define(nameof(SettingsDto.PlayedListNumberingStart), PlayedSongsSection, "Numbering direction",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.PlayedListNumberingStart,
            (settings, value) => settings.PlayedListNumberingStart = value),
        Define(nameof(SettingsDto.PlayedListPosition), PlayedSongsSection, "Panel position",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.PlayedListPosition,
            (settings, value) => settings.PlayedListPosition = value),
        Define(nameof(SettingsDto.PlayedListFontFamily), PlayedSongsSection, "Font",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.PlayedListFontFamily,
            (settings, value) => settings.PlayedListFontFamily = value),
        Define(nameof(SettingsDto.PlayedListFontSize), PlayedSongsSection, "Font size",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.PlayedListFontSize,
            (settings, value) => settings.PlayedListFontSize = value),
        Define(nameof(SettingsDto.PlayedListMaxLines), PlayedSongsSection, "Maximum lines",
            SettingsResetScope.PlayedSongsPanel,
            settings => settings.PlayedListMaxLines,
            (settings, value) => settings.PlayedListMaxLines = value),

        Define(nameof(SettingsDto.NowPlayingFields), NowPlayingSection, "Display fields",
            SettingsResetScope.NowPlayingPanel,
            settings => settings.NowPlayingFields,
            (settings, value) => settings.NowPlayingFields = value),
        Define(nameof(SettingsDto.NowPlayingPosition), NowPlayingSection, "Panel position",
            SettingsResetScope.NowPlayingPanel,
            settings => settings.NowPlayingPosition,
            (settings, value) => settings.NowPlayingPosition = value),
        Define(nameof(SettingsDto.NowPlayingFontFamily), NowPlayingSection, "Font",
            SettingsResetScope.NowPlayingPanel,
            settings => settings.NowPlayingFontFamily,
            (settings, value) => settings.NowPlayingFontFamily = value),
        Define(nameof(SettingsDto.NowPlayingFontSize), NowPlayingSection, "Font size",
            SettingsResetScope.NowPlayingPanel,
            settings => settings.NowPlayingFontSize,
            (settings, value) => settings.NowPlayingFontSize = value),
        Define(nameof(SettingsDto.NowPlayingWidth), NowPlayingSection, "Panel width",
            SettingsResetScope.NowPlayingPanel,
            settings => settings.NowPlayingWidth,
            (settings, value) => settings.NowPlayingWidth = value),

        Define(nameof(SettingsDto.WinnerDialogFields), WinnerDialogSection, "Display fields",
            SettingsResetScope.WinnerDialog,
            settings => settings.WinnerDialogFields,
            (settings, value) => settings.WinnerDialogFields = value),
        Define(nameof(SettingsDto.WinnerDialogFontFamily), WinnerDialogSection, "Font",
            SettingsResetScope.WinnerDialog,
            settings => settings.WinnerDialogFontFamily,
            (settings, value) => settings.WinnerDialogFontFamily = value),
        Define(nameof(SettingsDto.WinnerDialogFontSize), WinnerDialogSection, "Font size",
            SettingsResetScope.WinnerDialog,
            settings => settings.WinnerDialogFontSize,
            (settings, value) => settings.WinnerDialogFontSize = value),
        Define(nameof(SettingsDto.WinnerDialogWidth), WinnerDialogSection, "Dialog width",
            SettingsResetScope.WinnerDialog,
            settings => settings.WinnerDialogWidth,
            (settings, value) => settings.WinnerDialogWidth = value),
        Define(nameof(SettingsDto.WinnerDialogShowQueuePosition), WinnerDialogSection, "Queue position",
            SettingsResetScope.WinnerDialog,
            settings => settings.WinnerDialogShowQueuePosition,
            (settings, value) => settings.WinnerDialogShowQueuePosition = value),

        Define(nameof(SettingsDto.BackgroundMode), BackgroundSection, "Mode", SettingsResetScope.Background,
            settings => settings.BackgroundMode,
            (settings, value) => settings.BackgroundMode = value),
        Define(nameof(SettingsDto.BackgroundColor), BackgroundSection, "Color", SettingsResetScope.Background,
            settings => settings.BackgroundColor,
            (settings, value) => settings.BackgroundColor = value),
        Define(nameof(SettingsDto.BackgroundImage), BackgroundSection, "Image", SettingsResetScope.Background,
            settings => settings.BackgroundImage,
            (settings, value) => settings.BackgroundImage = value),

        Define(nameof(SettingsDto.WheelColors), WheelPaletteSection, "Wheel colors",
            SettingsResetScope.WheelPalette,
            settings => settings.WheelColors,
            (settings, value) => settings.WheelColors = value),

        Define(nameof(SettingsDto.ColorText), OverlayColorsSection, "Text color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorText,
            (settings, value) => settings.ColorText = value),
        Define(nameof(SettingsDto.ColorStatusBackground), OverlayColorsSection, "Status background color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorStatusBackground,
            (settings, value) => settings.ColorStatusBackground = value),
        Define(nameof(SettingsDto.ColorPlayedListBackground), OverlayColorsSection, "Panel background color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorPlayedListBackground,
            (settings, value) => settings.ColorPlayedListBackground = value),
        Define(nameof(SettingsDto.NowPlayingBackgroundOpacity), OverlayColorsSection, "Now Playing opacity",
            SettingsResetScope.OverlayColors,
            settings => settings.NowPlayingBackgroundOpacity,
            (settings, value) => settings.NowPlayingBackgroundOpacity = value),
        Define(nameof(SettingsDto.ColorPlayedItemBackground), OverlayColorsSection, "Played-song background color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorPlayedItemBackground,
            (settings, value) => settings.ColorPlayedItemBackground = value),
        Define(nameof(SettingsDto.ColorResizeHandleBackground), OverlayColorsSection, "Resize handle color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorResizeHandleBackground,
            (settings, value) => settings.ColorResizeHandleBackground = value),
        Define(nameof(SettingsDto.ColorResizeHandleHoverBackground), OverlayColorsSection,
            "Resize handle hover color", SettingsResetScope.OverlayColors,
            settings => settings.ColorResizeHandleHoverBackground,
            (settings, value) => settings.ColorResizeHandleHoverBackground = value),
        Define(nameof(SettingsDto.ColorToggleBackground), OverlayColorsSection, "Toggle background color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorToggleBackground,
            (settings, value) => settings.ColorToggleBackground = value),
        Define(nameof(SettingsDto.ColorButtonBackground), OverlayColorsSection, "Button background color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorButtonBackground,
            (settings, value) => settings.ColorButtonBackground = value),
        Define(nameof(SettingsDto.ColorButtonText), OverlayColorsSection, "Button text color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorButtonText,
            (settings, value) => settings.ColorButtonText = value),
        Define(nameof(SettingsDto.ColorPointer), OverlayColorsSection, "Wheel pointer color",
            SettingsResetScope.OverlayColors,
            settings => settings.ColorPointer,
            (settings, value) => settings.ColorPointer = value),

        Define(nameof(SettingsDto.DebugMode), AdvancedSection, "Diagnostic output", null,
            settings => settings.DebugMode,
            (settings, value) => settings.DebugMode = value)
    ];

    internal static IReadOnlyList<string> SupportedPropertyNames { get; } =
        Array.AsReadOnly(Definitions.Select(definition => definition.PropertyName).ToArray());

    public static string GetScopeLabel(SettingsResetScope scope)
    {
        return scope switch
        {
            SettingsResetScope.All => "All settings",
            SettingsResetScope.PlayedSongsPanel => PlayedSongsSection,
            SettingsResetScope.NowPlayingPanel => NowPlayingSection,
            SettingsResetScope.WinnerDialog => WinnerDialogSection,
            SettingsResetScope.Background => BackgroundSection,
            SettingsResetScope.WheelPalette => WheelPaletteSection,
            SettingsResetScope.OverlayColors => OverlayColorsSection,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown settings reset scope.")
        };
    }

    public static IReadOnlyList<SettingsResetField> GetAffectedFields(
        SettingsDto current,
        SettingsDto defaults,
        SettingsResetScope scope = SettingsResetScope.All)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(defaults);

        return SelectDefinitions(scope)
            .Where(definition => !Equals(definition.GetValue(current), definition.GetValue(defaults)))
            .Select(definition => new SettingsResetField(
                definition.PropertyName,
                definition.Section,
                definition.Label))
            .ToArray();
    }

    public static void ApplyDefaults(
        SettingsDto target,
        SettingsDto defaults,
        SettingsResetScope scope = SettingsResetScope.All)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(defaults);

        foreach (var definition in SelectDefinitions(scope))
            definition.ApplyDefault(target, defaults);
    }

    private static IEnumerable<FieldDefinition> SelectDefinitions(SettingsResetScope scope)
    {
        _ = GetScopeLabel(scope);
        return scope == SettingsResetScope.All
            ? Definitions
            : Definitions.Where(definition => definition.SubsetScope == scope);
    }

    private static FieldDefinition Define<T>(
        string propertyName,
        string section,
        string label,
        SettingsResetScope? subsetScope,
        Func<SettingsDto, T> getValue,
        Action<SettingsDto, T> setValue)
    {
        return new FieldDefinition(
            propertyName,
            section,
            label,
            subsetScope,
            settings => getValue(settings),
            (target, defaults) => setValue(target, getValue(defaults)));
    }

    private sealed record FieldDefinition(
        string PropertyName,
        string Section,
        string Label,
        SettingsResetScope? SubsetScope,
        Func<SettingsDto, object?> GetValue,
        Action<SettingsDto, SettingsDto> ApplyDefault);
}
