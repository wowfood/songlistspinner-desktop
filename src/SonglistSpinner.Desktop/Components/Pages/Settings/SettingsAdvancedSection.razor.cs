using Microsoft.AspNetCore.Components;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// The Settings page's Advanced section: diagnostics, resetting every setting, and the endpoints in use.
/// </summary>
public partial class SettingsAdvancedSection
{
    [Parameter, EditorRequired]
    public SettingsDto Dto { get; set; } = null!;

    [Parameter, EditorRequired]
    public bool ResetDialogOpen { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<SettingsResetScope> ResetRequested { get; set; }

    /// <summary>Opens the diagnostic log folder; the page reports a failure in its save bar.</summary>
    [Parameter, EditorRequired]
    public EventCallback OpenLogFolder { get; set; }
}
