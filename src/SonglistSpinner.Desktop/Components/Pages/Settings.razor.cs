using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Utilities;
using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.StreamerSongList;
using SonglistSpinner.Services;

namespace SonglistSpinner.Components.Pages;

// Injected properties come from the @inject directives in Settings.razor.
public partial class Settings
{
    private readonly SettingsViewModel _vm = new();
    private SettingsSection _activeSection = SettingsSection.Connection;
    private string _credentialClientId = "";
    private StreamerSongListCredentialKind _credentialKind = StreamerSongListCredentialKind.Streamer;
    private string? _credentialTestResult;
    private bool _credentialTestSucceeded;
    private string _credentialToken = "";
    private bool _clearingCredential;
    private CssValidationErrors _cssValidation = new();
    private SettingsDto? _dto;
    private EditContext? _editContext;
    private StreamerSongListCredential? _existingCredential;
    private bool _hasCredential;
    private bool _navigationPromptOpen;
    private bool _allowNavigation;
    private CancellationTokenSource? _previewRefreshCts;
    private bool _previewReady;
    private bool _resetDialogOpen;
    private string _playedListSeparatorChoice = SettingsOptions.CustomSeparatorKey;
    private string _nowPlayingSeparatorChoice = SettingsOptions.CustomSeparatorKey;
    private readonly SettingsDraftTracker _draftTracker = new();
    private bool _testingCredential;

    private string PreviewUrl => $"{OverlayServer.OverlayUrl}?preview=1";

    private bool HasUnsavedChanges => HasUnsavedSettingsChanges || HasUnsavedCredentialChanges;

    private bool HasUnsavedSettingsChanges =>
        _dto is not null && _draftTracker.HasUnsavedSettingsChanges(_dto, _vm);

    private bool HasUnsavedCredentialChanges => _draftTracker.HasUnsavedCredentialChanges(CredentialFormDraft);

    private CredentialDraft CredentialFormDraft => new(
        _credentialKind,
        _credentialClientId,
        TokenEntered: !string.IsNullOrWhiteSpace(_credentialToken));

    protected override async Task OnInitializedAsync()
    {
        ReplaceSettingsDraft(LocalSettings.LoadSettings());
        _existingCredential = await CredentialStore.GetCredentialAsync();
        if (_existingCredential is not null)
        {
            _credentialKind = _existingCredential.Kind;
            _credentialClientId = _existingCredential.ClientId ?? "";
            _hasCredential = true;
        }

        MarkDraftSaved();
    }

    public void Dispose()
    {
        if (_editContext is not null)
            _editContext.OnFieldChanged -= OnSettingsFieldChanged;

        _previewRefreshCts?.Cancel();
        _previewRefreshCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnSettingsFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        _vm.SaveSuccess = false;
        QueuePreviewRefresh();

        // The inputs live in the section components, whose bindings re-render only the section. The unsaved-draft
        // state in the header and save bar belongs to this page.
        StateHasChanged();
    }

    private void SelectSection(SettingsSection section)
    {
        _activeSection = section;
        _vm.SaveSuccess = false;
    }

    private string SectionClass(SettingsSection section) =>
        _activeSection == section ? "ss-settings-nav-item active" : "ss-settings-nav-item";

    private string PlayedListSeparatorChoice
    {
        get => _playedListSeparatorChoice;
        set
        {
            _playedListSeparatorChoice = value;
            if (_dto is null || !SettingsOptions.TryGetSeparator(value, out var separator)) return;
            _dto.PlayedListSeparator = separator;
            QueuePreviewRefresh();
        }
    }

    private string NowPlayingSeparatorChoice
    {
        get => _nowPlayingSeparatorChoice;
        set
        {
            _nowPlayingSeparatorChoice = value;
            if (_dto is null || !SettingsOptions.TryGetSeparator(value, out var separator)) return;
            _dto.NowPlayingSeparator = separator;
            QueuePreviewRefresh();
        }
    }

    private async Task OnPreviewLoadedAsync()
    {
        _previewReady = true;
        await PushPreviewAsync();
    }

    private void QueuePreviewRefresh()
    {
        _vm.SaveSuccess = false;
        if (!_previewReady || _dto is null) return;

        _previewRefreshCts?.Cancel();
        _previewRefreshCts?.Dispose();
        _previewRefreshCts = new CancellationTokenSource();
        PushPreviewAfterDelayAsync(_previewRefreshCts.Token)
            .ObserveFaults(ex => Logger.LogError(ex, "Refreshing the settings preview failed"));
    }

    private async Task PushPreviewAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(80), TimeProvider, cancellationToken);
            await InvokeAsync(PushPreviewAsync);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task PushPreviewAsync()
    {
        if (!_previewReady || _dto is null) return;

        try
        {
            var previewDto = JsonSerializer.Deserialize<SettingsDto>(JsonSerializer.Serialize(_dto))
                             ?? new SettingsDto();
            _vm.ApplyToDto(previewDto);
            var payload = SettingsPreview.CreatePayload(
                SettingsDtoConverter.ToSpinnerConfig(previewDto),
                previewDto.DefaultStreamerName);

            await JS.InvokeVoidAsync(
                SpinnerInteropMethods.UpdateSettingsPreview,
                "settingsOverlayPreview",
                payload);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            // The preview frame goes away when the user leaves the page, so a missed update is expected.
            Logger.LogDebug(ex, "Settings preview is unavailable");
        }
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

        if (!await ValidateCssSettingsAsync())
        {
            _vm.SaveError = "Correct the highlighted appearance values before saving.";
            SelectSection(_cssValidation.Has(nameof(SettingsDto.WheelColors))
                ? SettingsSection.Appearance
                : SettingsSection.Overlay);
            return false;
        }

        try
        {
            _vm.ApplyToDto(_dto);
            LocalSettings.SaveSettings(_dto);
            RefreshSeparatorChoices();
            DiagnosticLog.SetEnabled(_dto.DebugMode);
            StreamerSession.UpdateConfig(SettingsDtoConverter.ToSpinnerConfig(_dto));

            var credential = CredentialDraft.ToCredential(
                _credentialKind,
                _credentialToken,
                _credentialClientId,
                _existingCredential);
            if (credential is not null)
            {
                await CredentialStore.SaveCredentialAsync(credential);
                _existingCredential = credential;
                _credentialToken = "";
                _hasCredential = true;
            }

            _vm.SaveSuccess = true;
            MarkDraftSaved();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Saving settings failed");
            _vm.SaveError = ex.Message;
            return false;
        }
    }

    private async Task<bool> ValidateCssSettingsAsync()
    {
        if (_dto is null) return false;

        var wheelColors = _vm.WheelColorsRaw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new
        {
            sizes = new[]
            {
                new { key = nameof(SettingsDto.PlayedListFontSize), property = "font-size", value = _dto.PlayedListFontSize, label = "Played-list font size" },
                new { key = nameof(SettingsDto.NowPlayingWidth), property = "width", value = _dto.NowPlayingWidth, label = "Now Playing width" },
                new { key = nameof(SettingsDto.NowPlayingFontSize), property = "font-size", value = _dto.NowPlayingFontSize, label = "Now Playing font size" },
                new { key = nameof(SettingsDto.WinnerDialogWidth), property = "width", value = _dto.WinnerDialogWidth, label = "Winner dialog width" },
                new { key = nameof(SettingsDto.WinnerDialogFontSize), property = "font-size", value = _dto.WinnerDialogFontSize, label = "Winner dialog font size" }
            },
            colorLists = new[]
            {
                new { key = nameof(SettingsDto.WheelColors), label = "Wheel color", values = wheelColors }
            }
        };

        _cssValidation = new CssValidationErrors(await JS.InvokeAsync<Dictionary<string, string>>(
            SpinnerInteropMethods.ValidateCssSettings,
            request));
        return _cssValidation.IsEmpty;
    }

    private void ClearCssValidation(string key)
    {
        if (_cssValidation.Remove(key))
            _vm.SaveError = null;
    }

    private async Task ClearApiCredentialAsync()
    {
        if (!_hasCredential || _clearingCredential) return;

        _clearingCredential = true;
        try
        {
            var confirmed = await DialogService.ShowMessageBoxAsync(
                "Clear API credential?",
                "This permanently removes the saved StreamerSongList API credential from secure storage. " +
                "Your other settings and any unsaved settings draft will not be changed.",
                yesText: "Clear credential",
                cancelText: "Keep credential");

            if (confirmed != true) return;

            await CredentialStore.ClearCredentialAsync();
            _existingCredential = null;
            _credentialToken = "";
            _credentialClientId = "";
            _credentialKind = StreamerSongListCredentialKind.Streamer;
            _hasCredential = false;
            _credentialTestSucceeded = true;
            _credentialTestResult = "API credential cleared. Other settings were not changed.";
            _draftTracker.MarkCredentialSaved(CredentialFormDraft);
        }
        catch (Exception ex)
        {
            _credentialTestSucceeded = false;
            _credentialTestResult = $"The API credential could not be cleared: {ex.Message}";
        }
        finally
        {
            _clearingCredential = false;
        }
    }

    private async Task ReviewSettingsResetAsync(SettingsResetScope scope)
    {
        if (_dto is null || _resetDialogOpen) return;

        _vm.ApplyToDto(_dto);
        var defaults = CreateDefaultSettingsDraft();
        var scopeLabel = SettingsResetPlan.GetScopeLabel(scope);
        var affectedFields = SettingsResetPlan.GetAffectedFields(_dto, defaults, scope);
        if (affectedFields.Count == 0)
        {
            await DialogService.ShowMessageBoxAsync(
                $"{scopeLabel} already uses defaults",
                "No fields in this area differ from their shipped defaults. Nothing was changed, " +
                "and the API credential was not inspected.",
                yesText: "OK");
            return;
        }

        _resetDialogOpen = true;
        try
        {
            var parameters = new DialogParameters<ResetSettingsDialog>();
            parameters.Add(dialog => dialog.Fields, affectedFields);
            parameters.Add(dialog => dialog.ScopeLabel, scopeLabel);
            var options = new DialogOptions
            {
                BackdropClick = false,
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
                MaxWidth = MaxWidth.Small
            };
            var dialog = await DialogService.ShowAsync<ResetSettingsDialog>(
                $"Review {scopeLabel} reset",
                parameters,
                options);
            var result = await dialog.Result;
            if (result?.Canceled != false) return;

            SettingsResetPlan.ApplyDefaults(_dto, defaults, scope);
            ReplaceSettingsDraft(_dto);
        }
        finally
        {
            _resetDialogOpen = false;
        }
    }

    private static SettingsDto CreateDefaultSettingsDraft()
    {
        var defaults = new SettingsDto();
        var viewModel = new SettingsViewModel();
        viewModel.Initialize(defaults);
        viewModel.ApplyToDto(defaults);
        return defaults;
    }

    private void ReplaceSettingsDraft(SettingsDto settings)
    {
        if (_editContext is not null)
            _editContext.OnFieldChanged -= OnSettingsFieldChanged;

        _dto = settings;
        _vm.Initialize(_dto);
        RefreshSeparatorChoices();
        _editContext = new EditContext(_dto);
        _editContext.OnFieldChanged += OnSettingsFieldChanged;
        _vm.SaveSuccess = false;
        _vm.SaveError = null;
        QueuePreviewRefresh();
    }

    private void RefreshSeparatorChoices()
    {
        if (_dto is null) return;
        _playedListSeparatorChoice = SettingsOptions.GetSeparatorKey(_dto.PlayedListSeparator);
        _nowPlayingSeparatorChoice = SettingsOptions.GetSeparatorKey(_dto.NowPlayingSeparator);
    }


    private void OpenDiagnosticLogFolder()
    {
        try
        {
            Directory.CreateDirectory(DiagnosticLog.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = DiagnosticLog.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _vm.SaveError = $"The diagnostic log folder could not be opened: {ex.Message}";
        }
    }

    private async Task TestApiConnection()
    {
        if (_dto == null || _testingCredential) return;

        _testingCredential = true;
        _credentialTestResult = null;
        _credentialTestSucceeded = false;
        var previousCredential = _existingCredential;

        try
        {
            var streamerName = _dto.DefaultStreamerName.Trim();
            if (string.IsNullOrWhiteSpace(streamerName))
            {
                _credentialTestResult = "Enter a Default StreamerSongList Name before testing.";
                return;
            }

            if (!await SaveCoreAsync())
            {
                _credentialTestResult = $"Unable to save the credential: {_vm.SaveError}";
                return;
            }

            var platform = _dto.StreamerPlatform;
            var period = _dto.PlayHistoryPeriod;
            var (queueCount, historyCount) = await CredentialTest.RunAsync(
                previousCredential,
                _existingCredential,
                async cancellationToken =>
                {
                    var channel = new StreamerSongListChannel(streamerName, platform);
                    var fetched = await SongListClient.FetchQueueAndHistoryAsync(channel, period, cancellationToken);
                    return (fetched.Queue.Items.Length, fetched.PlayedSongs.Length);
                });
            _credentialTestSucceeded = true;
            _credentialTestResult =
                $"Connected to {ApiOptions.BaseAddress} and loaded {queueCount} queued song(s) " +
                $"and {historyCount} history item(s) for {streamerName}.";
        }
        catch (ApiCredentialTestFailedException ex)
        {
            if (ex.PreviousRestored)
                ShowSavedCredential(previousCredential);

            _credentialTestResult = $"Connection failed: {ex.Message}" +
                                    (ex.PreviousRestored
                                        ? " The previous credential was restored."
                                        : ex.RestoreFailureDescription is { } restoreFailure
                                            ? " " + restoreFailure
                                            : null);
            Logger.LogError(ex.InnerException, "API connection test failed");
        }
        catch (Exception ex)
        {
            // Validating the draft before saving it runs page script, which can fail before any credential changes.
            _credentialTestResult = $"Connection failed: {ex.Message}";
            Logger.LogError(ex, "API connection test failed");
        }
        finally
        {
            _testingCredential = false;
        }
    }

    private void ShowSavedCredential(StreamerSongListCredential? credential)
    {
        _existingCredential = credential;
        _hasCredential = credential is not null;
        _credentialKind = credential?.Kind ?? StreamerSongListCredentialKind.Streamer;
        _credentialClientId = credential?.ClientId ?? "";
        _credentialToken = "";
        _draftTracker.MarkCredentialSaved(CredentialFormDraft);
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

    private void MarkDraftSaved()
    {
        if (_dto is not null)
            _draftTracker.MarkSettingsSaved(_dto, _vm);
        _draftTracker.MarkCredentialSaved(CredentialFormDraft);
    }

    private enum SettingsSection
    {
        Connection,
        Spinner,
        Overlay,
        Appearance,
        Advanced
    }
}
