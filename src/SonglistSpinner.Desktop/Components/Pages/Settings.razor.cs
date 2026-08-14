using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor.Utilities;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Extensions;

namespace SonglistSpinner.Components.Pages;

// Injected properties (LocalSettings, Config) come from @inject in Settings.razor.
public partial class Settings
{
    private readonly SettingsViewModel _vm = new();
    private string _credentialClientId = "";
    private StreamerSongListCredentialKind _credentialKind = StreamerSongListCredentialKind.Streamer;
    private string? _credentialTestResult;
    private bool _credentialTestSucceeded;
    private string _credentialToken = "";
    private bool _clearCredentialOnSave;
    private SettingsDto? _dto;
    private StreamerSongListCredential? _existingCredential;
    private bool _hasCredential;
    private bool _navigationPromptOpen;
    private bool _allowNavigation;
    private string? _savedFormState;
    private bool _testingCredential;

    private bool HasUnsavedChanges =>
        _savedFormState is not null &&
        !StringComparer.Ordinal.Equals(_savedFormState, CaptureFormState());

    private MudColor ColorBackground
    {
        get => (_dto?.BackgroundColor ?? "#000000").ToMudColor();
        set
        {
            if (_dto != null) _dto.BackgroundColor = value.ToHexString();
        }
    }

    private MudColor ColorText
    {
        get => (_dto?.ColorText ?? "#000000").ToMudColor();
        set
        {
            if (_dto != null) _dto.ColorText = value.ToHexString();
        }
    }

    private MudColor ColorPointer
    {
        get => (_dto?.ColorPointer ?? "#000000").ToMudColor();
        set
        {
            if (_dto != null) _dto.ColorPointer = value.ToHexString();
        }
    }

    private MudColor ColorButtonBg
    {
        get => (_dto?.ColorButtonBackground ?? "#000000").ToMudColor();
        set
        {
            if (_dto != null) _dto.ColorButtonBackground = value.ToHexString();
        }
    }

    private MudColor ColorButtonText
    {
        get => (_dto?.ColorButtonText ?? "#000000").ToMudColor();
        set
        {
            if (_dto != null) _dto.ColorButtonText = value.ToHexString();
        }
    }

    private MudColor ColorPlayedListBg
    {
        get => _vm.PlayedListBgHex.ToMudColorWithAlpha(_vm.PlayedListBgAlpha);
        set
        {
            _vm.PlayedListBgHex = value.ToHexString();
            _vm.PlayedListBgAlpha = value.A / 255.0;
        }
    }

    private MudColor ColorPlayedItemBg
    {
        get => (_dto?.ColorPlayedItemBackground ?? "#000000").ToMudColor();
        set
        {
            if (_dto != null) _dto.ColorPlayedItemBackground = value.ToHexString();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _dto = LocalSettings.LoadSettings();
        _vm.Initialize(_dto);
        _existingCredential = await CredentialStore.GetCredentialAsync();
        if (_existingCredential is not null)
        {
            _credentialKind = _existingCredential.Kind;
            _credentialClientId = _existingCredential.ClientId ?? "";
            _hasCredential = true;
        }

        _savedFormState = CaptureFormState();
    }

    private Task Save()
    {
        return SaveCoreAsync();
    }

    private async Task<bool> SaveCoreAsync()
    {
        _vm.SaveSuccess = false;
        _vm.SaveError = null;
        if (_dto == null) return false;

        try
        {
            _vm.ApplyToDto(_dto);
            LocalSettings.SaveSettings(_dto);

            var submittedToken = _credentialToken.Trim();
            if (_clearCredentialOnSave && string.IsNullOrWhiteSpace(submittedToken))
            {
                await CredentialStore.ClearCredentialAsync();
                _existingCredential = null;
                _clearCredentialOnSave = false;
                _hasCredential = false;
            }
            else
            {
                var token = string.IsNullOrWhiteSpace(submittedToken)
                    ? _existingCredential?.Token
                    : submittedToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _existingCredential = new StreamerSongListCredential(
                        _credentialKind,
                        token,
                        string.IsNullOrWhiteSpace(_credentialClientId) ? null : _credentialClientId.Trim());
                    await CredentialStore.SaveCredentialAsync(_existingCredential);
                    _credentialToken = "";
                    _hasCredential = true;
                    _clearCredentialOnSave = false;
                }
            }

            _vm.SaveSuccess = true;
            _savedFormState = CaptureFormState();
            return true;
        }
        catch (Exception ex)
        {
            _vm.SaveError = ex.Message;
            return false;
        }
    }

    private void ClearApiCredential()
    {
        _credentialToken = "";
        _credentialClientId = "";
        _credentialKind = StreamerSongListCredentialKind.Streamer;
        _clearCredentialOnSave = true;
        _hasCredential = false;
        _credentialTestResult = null;
        _credentialTestSucceeded = false;
    }

    private async Task TestApiConnection()
    {
        if (_dto == null || _testingCredential) return;

        _testingCredential = true;
        _credentialTestResult = null;
        _credentialTestSucceeded = false;

        try
        {
            if (!await SaveCoreAsync())
            {
                _credentialTestResult = $"Unable to save the credential: {_vm.SaveError}";
                return;
            }

            var streamerName = _dto.DefaultStreamerName.Trim();
            if (string.IsNullOrWhiteSpace(streamerName))
            {
                _credentialTestResult = "Enter a Default StreamerSongList Name before testing.";
                return;
            }

            var channel = new StreamerSongListChannel(streamerName, _dto.StreamerPlatform);
            var queue = await ApiService.FetchQueueAsync(channel);
            var history = await ApiService.FetchPlayHistoryAsync(channel, _dto.PlayHistoryPeriod);
            _credentialTestSucceeded = true;
            _credentialTestResult =
                $"Connected to {ApiOptions.BaseAddress} and loaded {queue.Length} queued song(s) " +
                $"and {history.Length} history item(s) for {streamerName}.";
        }
        catch (Exception ex)
        {
            _credentialTestResult = $"Connection failed: {ex.Message}";
            Debug.WriteLine($"[SonglistSpinner] API connection test failed: {ex}");
        }
        finally
        {
            _testingCredential = false;
        }
    }

    private async Task ConfirmNavigationAsync(LocationChangingContext context)
    {
        if (_allowNavigation || !HasUnsavedChanges) return;

        context.PreventNavigation();
        if (_navigationPromptOpen) return;

        _navigationPromptOpen = true;
        try
        {
            var choice = await DialogService.ShowMessageBoxAsync(
                "Unsaved settings",
                "Settings have been changed. Please save them before leaving, or abandon your changes.",
                yesText: "Save and leave",
                noText: "Abandon changes",
                cancelText: "Keep editing");

            if (choice == true)
            {
                if (!await SaveCoreAsync()) return;
            }
            else if (choice is not false)
            {
                return;
            }

            _allowNavigation = true;
            Navigation.NavigateTo(context.TargetLocation);
        }
        finally
        {
            _navigationPromptOpen = false;
        }
    }

    private string CaptureFormState()
    {
        if (_dto is null) return "";

        return JsonSerializer.Serialize(new SettingsFormSnapshot(
            JsonSerializer.Serialize(_dto),
            _vm.WheelColorsRaw,
            CaptureDisplayFields(_vm.DisplayFields),
            CaptureDisplayFields(_vm.NowPlayingDisplayFields),
            _vm.PlayedListBgHex,
            _vm.PlayedListBgAlpha,
            _credentialKind,
            _credentialClientId,
            _credentialToken,
            _hasCredential,
            _clearCredentialOnSave));
    }

    private static string CaptureDisplayFields(IEnumerable<DisplayField> fields)
    {
        return JsonSerializer.Serialize(fields.Select(field => new DisplayFieldSnapshot(field.Name, field.Selected)));
    }

    private sealed record DisplayFieldSnapshot(string Name, bool Selected);

    private sealed record SettingsFormSnapshot(
        string Settings,
        string WheelColors,
        string DisplayFields,
        string NowPlayingDisplayFields,
        string PlayedListBackground,
        double PlayedListBackgroundAlpha,
        StreamerSongListCredentialKind CredentialKind,
        string CredentialClientId,
        string CredentialToken,
        bool HasCredential,
        bool ClearCredentialOnSave);
}
