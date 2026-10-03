using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;

namespace SonglistSpinner.Components.Pages;

/// <summary>
/// The Settings page's Connection section: the default channel and the API credential fields. The credential
/// fields, their status and the save-and-test and clear actions belong to the page, which tracks them as part of the
/// unsaved draft.
/// </summary>
public partial class SettingsConnectionSection
{
    [Parameter, EditorRequired]
    public SettingsDto Dto { get; set; } = null!;

    [Parameter, EditorRequired]
    public StreamerSongListCredentialKind CredentialKind { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<StreamerSongListCredentialKind> CredentialKindChanged { get; set; }

    [Parameter, EditorRequired]
    public Expression<Func<StreamerSongListCredentialKind>> CredentialKindExpression { get; set; } = null!;

    [Parameter, EditorRequired]
    public string CredentialToken { get; set; } = "";

    [Parameter, EditorRequired]
    public EventCallback<string> CredentialTokenChanged { get; set; }

    [Parameter, EditorRequired]
    public Expression<Func<string>> CredentialTokenExpression { get; set; } = null!;

    [Parameter, EditorRequired]
    public string CredentialClientId { get; set; } = "";

    [Parameter, EditorRequired]
    public EventCallback<string> CredentialClientIdChanged { get; set; }

    [Parameter, EditorRequired]
    public Expression<Func<string>> CredentialClientIdExpression { get; set; } = null!;

    [Parameter, EditorRequired]
    public bool HasCredential { get; set; }

    [Parameter, EditorRequired]
    public bool ClearingCredential { get; set; }

    [Parameter, EditorRequired]
    public bool TestingCredential { get; set; }

    [Parameter, EditorRequired]
    public string? CredentialTestResult { get; set; }

    [Parameter, EditorRequired]
    public bool CredentialTestSucceeded { get; set; }

    [Parameter, EditorRequired]
    public EventCallback ClearCredential { get; set; }

    [Parameter, EditorRequired]
    public EventCallback TestConnection { get; set; }

    private void OpenSetupWizard()
    {
        Navigation.NavigateTo("/setup");
    }
}
