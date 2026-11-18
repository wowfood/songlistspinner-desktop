using Microsoft.AspNetCore.Components;
using SonglistSpinner.Core.Settings;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// The Settings page's Spinner &amp; Queue section: winner actions and queue filtering.
/// </summary>
public partial class SettingsSpinnerSection
{
    [Parameter, EditorRequired]
    public SettingsDto Dto { get; set; } = null!;
}
