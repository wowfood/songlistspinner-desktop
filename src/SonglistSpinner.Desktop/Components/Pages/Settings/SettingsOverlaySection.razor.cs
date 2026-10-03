using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Services;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// The Settings page's Overlay Layout section: the played-songs panel, the Now Playing panel and the winner dialog.
/// The separator choices belong to the page, because a "Custom" choice is not stored in the settings and must
/// survive switching sections.
/// </summary>
public partial class SettingsOverlaySection
{
    private static readonly (string Value, string Label)[] FontChoices =
    [
        ("sans-serif", "Sans-serif"),
        ("serif", "Serif"),
        ("monospace", "Monospace"),
        ("Arial", "Arial"),
        ("Helvetica", "Helvetica"),
        ("Verdana", "Verdana"),
        ("Georgia", "Georgia"),
        ("'Courier New'", "Courier New")
    ];

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

    [Parameter, EditorRequired]
    public string PlayedListSeparatorChoice { get; set; } = "";

    [Parameter, EditorRequired]
    public EventCallback<string> PlayedListSeparatorChoiceChanged { get; set; }

    [Parameter, EditorRequired]
    public Expression<Func<string>> PlayedListSeparatorChoiceExpression { get; set; } = null!;

    [Parameter, EditorRequired]
    public string NowPlayingSeparatorChoice { get; set; } = "";

    [Parameter, EditorRequired]
    public EventCallback<string> NowPlayingSeparatorChoiceChanged { get; set; }

    [Parameter, EditorRequired]
    public Expression<Func<string>> NowPlayingSeparatorChoiceExpression { get; set; } = null!;

    [Parameter, EditorRequired]
    public EventCallback OpenWinnerSettings { get; set; }

    /// <summary>Raised after a field editor changes the draft, which the edit context does not see.</summary>
    [Parameter, EditorRequired]
    public EventCallback DraftChanged { get; set; }

    private bool PlayedListUsesCustomSeparator =>
        string.Equals(
            PlayedListSeparatorChoice,
            SettingsOptions.CustomSeparatorKey,
            StringComparison.Ordinal);

    private bool NowPlayingUsesCustomSeparator =>
        string.Equals(
            NowPlayingSeparatorChoice,
            SettingsOptions.CustomSeparatorKey,
            StringComparison.Ordinal);

    private Task MovePlayedField(DisplayFieldOrderChange change) =>
        NotifyIfChanged(ViewModel.MoveField(change.FieldName, change.NewIndex));

    private Task TogglePlayedField(string fieldName) =>
        NotifyIfChanged(ViewModel.ToggleField(fieldName));

    private Task MoveNowPlayingField(DisplayFieldOrderChange change) =>
        NotifyIfChanged(ViewModel.MoveNowPlayingField(change.FieldName, change.NewIndex));

    private Task ToggleNowPlayingField(string fieldName) =>
        NotifyIfChanged(ViewModel.ToggleNowPlayingField(fieldName));

    private Task MoveWinnerDialogField(DisplayFieldOrderChange change) =>
        NotifyIfChanged(ViewModel.MoveWinnerDialogField(change.FieldName, change.NewIndex));

    private Task ToggleWinnerDialogField(string fieldName) =>
        NotifyIfChanged(ViewModel.ToggleWinnerDialogField(fieldName));

    private Task NotifyIfChanged(bool changed) => changed ? DraftChanged.InvokeAsync() : Task.CompletedTask;
}
