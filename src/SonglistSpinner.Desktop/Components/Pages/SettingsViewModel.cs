using System.Text.Json;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Components.Pages;

public sealed class SettingsViewModel
{
    // The pointer colour picker needs hex; an unrecognised saved value falls back to the default pointer.
    private static readonly string DefaultPointerHex =
        CssNamedColors.ToHex(SpinnerDefaults.Colors.Pointer, PanelBackgroundColor.DefaultHex);

    public string WheelColorsRaw { get; set; } = "";
    public bool SaveSuccess { get; set; }
    public string? SaveError { get; set; }
    public List<DisplayField> DisplayFields { get; private set; } = new();
    public List<DisplayField> NowPlayingDisplayFields { get; private set; } = new();
    public List<DisplayField> WinnerDialogDisplayFields { get; private set; } = new();
    public string PlayedListBgHex { get; set; } = PanelBackgroundColor.DefaultHex;
    public double PlayedListBgAlpha { get; set; } = PanelBackgroundColor.DefaultOpacity;
    public bool UseIndependentNowPlayingBgAlpha { get; set; }
    public double NowPlayingBgAlpha { get; set; } = PanelBackgroundColor.DefaultOpacity;

    public void Initialize(SettingsDto dto)
    {
        SettingsDtoNormalizer.NormalizeInPlace(dto);

        try
        {
            WheelColorsRaw = string.Join("\n", JsonSerializer.Deserialize<string[]>(dto.WheelColors) ?? []);
        }
        catch
        {
            WheelColorsRaw = string.Join("\n", SpinnerDefaults.CreateWheelColors());
        }

        InitDisplayFields(dto.PlayedListFields);
        InitNowPlayingDisplayFields(dto.NowPlayingFields);
        InitWinnerDialogDisplayFields(dto.WinnerDialogFields, dto.PlayedListFields);
        InitPlayedListBg(dto.ColorPlayedListBackground);
        UseIndependentNowPlayingBgAlpha = dto.NowPlayingBackgroundOpacity.HasValue;
        NowPlayingBgAlpha = PanelBackgroundColor.ClampOpacity(
            dto.NowPlayingBackgroundOpacity ?? PlayedListBgAlpha);
        dto.ColorPointer = CssNamedColors.ToHex(dto.ColorPointer, DefaultPointerHex);
    }

    public void InitDisplayFields(string json)
    {
        DisplayFields = BuildDisplayFields(json);
    }

    public void InitNowPlayingDisplayFields(string json)
    {
        NowPlayingDisplayFields = BuildDisplayFields(json);
    }

    public void InitWinnerDialogDisplayFields(string? json, string legacyPlayedListFields)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            WinnerDialogDisplayFields = BuildDisplayFields(json, SongFieldNames.CreateWinnerDefaultSelection());
            return;
        }

        WinnerDialogDisplayFields = BuildDisplayFields(legacyPlayedListFields);
        var requester = WinnerDialogDisplayFields.First(field => field.Name == SongFieldNames.Requester);
        requester.Selected = true;
    }

    private static List<DisplayField> BuildDisplayFields(string json, string[]? fallback = null)
    {
        var selected = SettingsDtoNormalizer.ParseFields(
            json,
            fallback ?? SongFieldNames.CreateDefaultSelection());

        return selected
            .Select(f => new DisplayField { Name = f, Selected = true })
            .Concat(SongFieldNames.Values
                .Except(selected, StringComparer.OrdinalIgnoreCase)
                .Select(f => new DisplayField { Name = f, Selected = false }))
            .ToList();
    }

    public bool ToggleField(string fieldName) =>
        ToggleField(DisplayFields, fieldName);

    public bool ToggleNowPlayingField(string fieldName) =>
        ToggleField(NowPlayingDisplayFields, fieldName);

    public bool ToggleWinnerDialogField(string fieldName) =>
        ToggleField(WinnerDialogDisplayFields, fieldName);

    public bool MoveField(string fieldName, int targetIndex) =>
        MoveField(DisplayFields, fieldName, targetIndex);

    public bool MoveNowPlayingField(string fieldName, int targetIndex) =>
        MoveField(NowPlayingDisplayFields, fieldName, targetIndex);

    public bool MoveWinnerDialogField(string fieldName, int targetIndex) =>
        MoveField(WinnerDialogDisplayFields, fieldName, targetIndex);

    private static bool ToggleField(List<DisplayField> fields, string fieldName)
    {
        var field = fields.FirstOrDefault(
            candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            return false;
        }

        field.Selected = !field.Selected;
        return true;
    }

    private static bool MoveField(List<DisplayField> fields, string fieldName, int targetIndex)
    {
        var sourceIndex = fields.FindIndex(
            candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (sourceIndex < 0 || fields.Count < 2)
        {
            return false;
        }

        targetIndex = Math.Clamp(targetIndex, 0, fields.Count - 1);
        if (sourceIndex == targetIndex)
        {
            return false;
        }

        var field = fields[sourceIndex];
        fields.RemoveAt(sourceIndex);
        fields.Insert(targetIndex, field);
        return true;
    }

    public void InitPlayedListBg(string value)
    {
        var color = PanelBackgroundColor.Parse(value);
        PlayedListBgHex = color.Hex;
        PlayedListBgAlpha = color.Opacity;
    }

    public string ComputedPlayedListBg()
    {
        return new PanelBackgroundColor(PlayedListBgHex, PlayedListBgAlpha).ToCss();
    }

    public string ComputedNowPlayingBg()
    {
        var opacity = UseIndependentNowPlayingBgAlpha ? NowPlayingBgAlpha : PlayedListBgAlpha;
        return new PanelBackgroundColor(PlayedListBgHex, opacity).ToCss();
    }

    public void ApplyToDto(SettingsDto dto)
    {
        var colors = WheelColorsRaw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToArray();
        dto.WheelColors = JsonSerializer.Serialize(colors);

        var fields = DisplayFields.Where(f => f.Selected).Select(f => f.Name).ToArray();
        dto.PlayedListFields = JsonSerializer.Serialize(
            SongFieldNames.NormalizeSelection(fields, SongFieldNames.CreateDefaultSelection()));

        var nowPlayingFields = NowPlayingDisplayFields.Where(f => f.Selected).Select(f => f.Name).ToArray();
        dto.NowPlayingFields = JsonSerializer.Serialize(
            SongFieldNames.NormalizeSelection(nowPlayingFields, SongFieldNames.CreateDefaultSelection()));

        var winnerDialogFields = WinnerDialogDisplayFields.Where(f => f.Selected).Select(f => f.Name).ToArray();
        dto.WinnerDialogFields = JsonSerializer.Serialize(
            SongFieldNames.NormalizeSelection(
                winnerDialogFields,
                SongFieldNames.CreateWinnerDefaultSelection()));

        dto.ColorPlayedListBackground = ComputedPlayedListBg();
        dto.NowPlayingBackgroundOpacity = UseIndependentNowPlayingBgAlpha
            ? PanelBackgroundColor.ClampOpacity(NowPlayingBgAlpha)
            : null;
    }
}
