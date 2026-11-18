using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Services;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// The Settings page's Appearance section: background, wheel palette and overlay colours, with a readability check
/// on the colour pairs viewers read text against.
/// </summary>
public partial class SettingsAppearanceSection
{
    [Parameter, EditorRequired]
    public SettingsDto Dto { get; set; } = null!;

    [Parameter, EditorRequired]
    public SettingsViewModel ViewModel { get; set; } = null!;

    [Parameter, EditorRequired]
    public CssValidationErrors CssValidation { get; set; } = null!;

    /// <summary>Raised with the <see cref="SettingsDto"/> property name of a CSS field the user is editing.</summary>
    [Parameter, EditorRequired]
    public EventCallback<string> CssValidationCleared { get; set; }

    [Parameter, EditorRequired]
    public bool ResetDialogOpen { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<SettingsResetScope> ResetRequested { get; set; }

    /// <summary>Raised after a colour picker or opacity control changes the draft, which the edit context does not see.</summary>
    [Parameter, EditorRequired]
    public EventCallback DraftChanged { get; set; }

    private MudColor ColorBackground
    {
        get => Dto.BackgroundColor.ToMudColor();
        set => Dto.BackgroundColor = value.ToHexString();
    }

    private MudColor ColorText
    {
        get => Dto.ColorText.ToMudColor();
        set => Dto.ColorText = value.ToHexString();
    }

    private MudColor ColorPointer
    {
        get => Dto.ColorPointer.ToMudColor();
        set => Dto.ColorPointer = value.ToHexString();
    }

    private MudColor ColorButtonBg
    {
        get => Dto.ColorButtonBackground.ToMudColor();
        set => Dto.ColorButtonBackground = value.ToHexString();
    }

    private MudColor ColorButtonText
    {
        get => Dto.ColorButtonText.ToMudColor();
        set => Dto.ColorButtonText = value.ToHexString();
    }

    private MudColor ColorPlayedListBg
    {
        get => ViewModel.PlayedListBgHex.ToMudColor();
        set => ViewModel.PlayedListBgHex = value.ToHexString();
    }

    private MudColor ColorPlayedItemBg
    {
        get => Dto.ColorPlayedItemBackground.ToMudColor();
        set => Dto.ColorPlayedItemBackground = value.ToHexString();
    }

    private int PlayedListOpacityPercent
    {
        get => (int)Math.Round(ViewModel.PlayedListBgAlpha * 100, MidpointRounding.AwayFromZero);
        set => ViewModel.PlayedListBgAlpha = Math.Clamp(value, 0, 100) / 100.0;
    }

    private bool UseIndependentNowPlayingOpacity
    {
        get => ViewModel.UseIndependentNowPlayingBgAlpha;
        set
        {
            if (value && !ViewModel.UseIndependentNowPlayingBgAlpha)
                ViewModel.NowPlayingBgAlpha = ViewModel.PlayedListBgAlpha;

            ViewModel.UseIndependentNowPlayingBgAlpha = value;
        }
    }

    private int NowPlayingOpacityPercent
    {
        get
        {
            var opacity = ViewModel.UseIndependentNowPlayingBgAlpha
                ? ViewModel.NowPlayingBgAlpha
                : ViewModel.PlayedListBgAlpha;
            return (int)Math.Round(opacity * 100, MidpointRounding.AwayFromZero);
        }
        set => ViewModel.NowPlayingBgAlpha = Math.Clamp(value, 0, 100) / 100.0;
    }

    private string? ContrastWarning
    {
        get
        {
            var lowContrastPairs = new List<string>();
            if (ContrastRatio(Dto.ColorText, Dto.ColorPlayedItemBackground) < 4.5)
                lowContrastPairs.Add("overlay text on played-song cards");
            if (ContrastRatio(Dto.ColorButtonText, Dto.ColorButtonBackground) < 4.5)
                lowContrastPairs.Add("button text on button backgrounds");

            return lowContrastPairs.Count == 0
                ? null
                : $"Increase the contrast for {string.Join(" and ", lowContrastPairs)}. Aim for at least 4.5:1 for normal text.";
        }
    }

    private Task NotifyDraftChanged() => DraftChanged.InvokeAsync();

    private Task ClearWheelColorsValidation() => CssValidationCleared.InvokeAsync(nameof(SettingsDto.WheelColors));

    private static double ContrastRatio(string foreground, string background)
    {
        if (!TryParseHexColor(foreground, out var foregroundRgb) ||
            !TryParseHexColor(background, out var backgroundRgb))
            return double.MaxValue;

        var foregroundLuminance = RelativeLuminance(foregroundRgb);
        var backgroundLuminance = RelativeLuminance(backgroundRgb);
        return (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05) /
               (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);
    }

    private static bool TryParseHexColor(string? value, out (byte Red, byte Green, byte Blue) color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var hex = value.Trim();
        if (hex.Length != 7 || hex[0] != '#') return false;
        try
        {
            color = (
                Convert.ToByte(hex[1..3], 16),
                Convert.ToByte(hex[3..5], 16),
                Convert.ToByte(hex[5..7], 16));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static double RelativeLuminance((byte Red, byte Green, byte Blue) color)
    {
        static double Linearize(byte component)
        {
            var channel = component / 255d;
            return channel <= 0.04045
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(color.Red) +
               0.7152 * Linearize(color.Green) +
               0.0722 * Linearize(color.Blue);
    }
}
