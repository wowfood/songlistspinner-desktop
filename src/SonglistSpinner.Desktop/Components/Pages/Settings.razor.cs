using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor.Utilities;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
using SonglistSpinner.Extensions;
using SonglistSpinner.Services;

namespace SonglistSpinner.Components.Pages;

// Injected properties (LocalSettings, Config) come from @inject in Settings.razor.
public partial class Settings
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

    private static readonly SpinnerQueueItem[] PreviewSongs =
    [
        CreatePreviewSong(1, "The Midnight", "Sunset", "mod_jane", 10),
        CreatePreviewSong(2, "CHVRCHES", "Clearest Blue", "musicfan"),
        CreatePreviewSong(3, "Daft Punk", "Digital Love", "alex"),
        CreatePreviewSong(4, "Florence + The Machine", "Dog Days Are Over", "streamviewer")
    ];

    private readonly SettingsViewModel _vm = new();
    private SettingsSection _activeSection = SettingsSection.Connection;
    private string _credentialClientId = "";
    private StreamerSongListCredentialKind _credentialKind = StreamerSongListCredentialKind.Streamer;
    private string? _credentialTestResult;
    private bool _credentialTestSucceeded;
    private string _credentialToken = "";
    private bool _clearCredentialOnSave;
    private SettingsDto? _dto;
    private EditContext? _editContext;
    private StreamerSongListCredential? _existingCredential;
    private bool _hasCredential;
    private bool _navigationPromptOpen;
    private bool _allowNavigation;
    private CancellationTokenSource? _previewRefreshCts;
    private bool _previewReady;
    private string? _savedFormState;
    private bool _testingCredential;

    private string PreviewUrl => $"{OverlayService.OverlayUrl}?preview=1";

    private string WheelColorsRaw
    {
        get => _vm.WheelColorsRaw;
        set
        {
            _vm.WheelColorsRaw = value;
            QueuePreviewRefresh();
        }
    }

    private bool HasUnsavedChanges =>
        _savedFormState is not null &&
        !StringComparer.Ordinal.Equals(_savedFormState, CaptureFormState());

    private MudColor ColorBackground
    {
        get => (_dto?.BackgroundColor ?? "#000000").ToMudColor();
        set
        {
            if (_dto == null) return;
            _dto.BackgroundColor = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    private MudColor ColorText
    {
        get => (_dto?.ColorText ?? "#000000").ToMudColor();
        set
        {
            if (_dto == null) return;
            _dto.ColorText = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    private MudColor ColorPointer
    {
        get => (_dto?.ColorPointer ?? "#000000").ToMudColor();
        set
        {
            if (_dto == null) return;
            _dto.ColorPointer = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    private MudColor ColorButtonBg
    {
        get => (_dto?.ColorButtonBackground ?? "#000000").ToMudColor();
        set
        {
            if (_dto == null) return;
            _dto.ColorButtonBackground = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    private MudColor ColorButtonText
    {
        get => (_dto?.ColorButtonText ?? "#000000").ToMudColor();
        set
        {
            if (_dto == null) return;
            _dto.ColorButtonText = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    private MudColor ColorPlayedListBg
    {
        get => _vm.PlayedListBgHex.ToMudColor();
        set
        {
            _vm.PlayedListBgHex = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    private int PlayedListOpacityPercent
    {
        get => (int)Math.Round(_vm.PlayedListBgAlpha * 100, MidpointRounding.AwayFromZero);
        set
        {
            _vm.PlayedListBgAlpha = Math.Clamp(value, 0, 100) / 100.0;
            QueuePreviewRefresh();
        }
    }

    private bool UseIndependentNowPlayingOpacity
    {
        get => _vm.UseIndependentNowPlayingBgAlpha;
        set
        {
            if (value && !_vm.UseIndependentNowPlayingBgAlpha)
                _vm.NowPlayingBgAlpha = _vm.PlayedListBgAlpha;

            _vm.UseIndependentNowPlayingBgAlpha = value;
            QueuePreviewRefresh();
        }
    }

    private int NowPlayingOpacityPercent
    {
        get
        {
            var opacity = _vm.UseIndependentNowPlayingBgAlpha
                ? _vm.NowPlayingBgAlpha
                : _vm.PlayedListBgAlpha;
            return (int)Math.Round(opacity * 100, MidpointRounding.AwayFromZero);
        }
        set
        {
            _vm.NowPlayingBgAlpha = Math.Clamp(value, 0, 100) / 100.0;
            QueuePreviewRefresh();
        }
    }

    private MudColor ColorPlayedItemBg
    {
        get => (_dto?.ColorPlayedItemBackground ?? "#000000").ToMudColor();
        set
        {
            if (_dto == null) return;
            _dto.ColorPlayedItemBackground = value.ToHexString();
            QueuePreviewRefresh();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _dto = LocalSettings.LoadSettings();
        _vm.Initialize(_dto);
        _editContext = new EditContext(_dto);
        _editContext.OnFieldChanged += OnSettingsFieldChanged;
        _existingCredential = await CredentialStore.GetCredentialAsync();
        if (_existingCredential is not null)
        {
            _credentialKind = _existingCredential.Kind;
            _credentialClientId = _existingCredential.ClientId ?? "";
            _hasCredential = true;
        }

        _savedFormState = CaptureFormState();
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
    }

    private void SelectSection(SettingsSection section)
    {
        _activeSection = section;
        _vm.SaveSuccess = false;
    }

    private string SectionClass(SettingsSection section) =>
        _activeSection == section ? "ss-settings-nav-item active" : "ss-settings-nav-item";

    private static string FieldLabel(string field) => SettingsOptions.GetSongFieldLabel(field);

    private void BeginPlayedFieldDrag(int index)
    {
        _vm.DragIdx = index;
        _vm.DragOverIdx = -1;
    }

    private void SetPlayedFieldDragOver(int index)
    {
        if (_vm.DragOverIdx != index) _vm.DragOverIdx = index;
    }

    private void DropPlayedField(int index)
    {
        _vm.DropField(index);
        QueuePreviewRefresh();
    }

    private void EndPlayedFieldDrag()
    {
        _vm.DragIdx = -1;
        _vm.DragOverIdx = -1;
    }

    private void TogglePlayedField(int index)
    {
        _vm.ToggleField(index);
        QueuePreviewRefresh();
    }

    private void BeginNowPlayingFieldDrag(int index)
    {
        _vm.NowPlayingDragIdx = index;
        _vm.NowPlayingDragOverIdx = -1;
    }

    private void SetNowPlayingFieldDragOver(int index)
    {
        if (_vm.NowPlayingDragOverIdx != index) _vm.NowPlayingDragOverIdx = index;
    }

    private void DropNowPlayingField(int index)
    {
        _vm.DropNowPlayingField(index);
        QueuePreviewRefresh();
    }

    private void EndNowPlayingFieldDrag()
    {
        _vm.NowPlayingDragIdx = -1;
        _vm.NowPlayingDragOverIdx = -1;
    }

    private void ToggleNowPlayingField(int index)
    {
        _vm.ToggleNowPlayingField(index);
        QueuePreviewRefresh();
    }

    private void BeginWinnerDialogFieldDrag(int index)
    {
        _vm.WinnerDialogDragIdx = index;
        _vm.WinnerDialogDragOverIdx = -1;
    }

    private void SetWinnerDialogFieldDragOver(int index)
    {
        if (_vm.WinnerDialogDragOverIdx != index) _vm.WinnerDialogDragOverIdx = index;
    }

    private void DropWinnerDialogField(int index)
    {
        _vm.DropWinnerDialogField(index);
        QueuePreviewRefresh();
    }

    private void EndWinnerDialogFieldDrag()
    {
        _vm.WinnerDialogDragIdx = -1;
        _vm.WinnerDialogDragOverIdx = -1;
    }

    private void ToggleWinnerDialogField(int index)
    {
        _vm.ToggleWinnerDialogField(index);
        QueuePreviewRefresh();
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
        _ = PushPreviewAfterDelayAsync(_previewRefreshCts.Token);
    }

    private async Task PushPreviewAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
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
            var config = LocalSettings.ToSpinnerConfig(previewDto);
            var nowPlayingFields = config.NowPlaying.Fields is { Length: > 0 }
                ? config.NowPlaying.Fields
                : SongFieldNames.CreateDefaultSelection();

            var payload = new
            {
                config,
                streamer = string.IsNullOrWhiteSpace(previewDto.DefaultStreamerName)
                    ? "your-channel"
                    : previewDto.DefaultStreamerName.Trim(),
                wheelItems = PreviewSongs.Select(song => new { label = SpinnerDataService.BuildWheelLabel(song) }),
                playedTexts = SpinnerDataService.CreatePlayedSongTexts(PreviewSongs.Take(3).ToArray(), config),
                nowPlayingText = SpinnerDataService.CreateSongTextForFields(PreviewSongs[3], nowPlayingFields),
                playedCount = 3,
                availableCount = PreviewSongs.Length
            };

            await JS.InvokeVoidAsync(
                SpinnerInteropMethods.UpdateSettingsPreview,
                "settingsOverlayPreview",
                payload);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            Trace.WriteLine($"[SonglistSpinner] Settings preview is unavailable: {ex.Message}");
        }
    }

    private static SpinnerQueueItem CreatePreviewSong(
        int id,
        string artist,
        string title,
        string requester,
        decimal? donation = null)
    {
        return new SpinnerQueueItem
        {
            QueueId = id,
            Position = id,
            Song = new SpinnerSong { Id = id, Artist = artist, Title = title },
            Requests = [new SpinnerRequest { Name = requester, DonationAmount = donation }]
        };
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
            DiagnosticLog.Configure(_dto.DebugMode);
            await OverlayService.UpdateConfigAsync(LocalSettings.ToSpinnerConfig(_dto));

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
                    var credential = new StreamerSongListCredential(
                        _credentialKind,
                        token,
                        string.IsNullOrWhiteSpace(_credentialClientId) ? null : _credentialClientId.Trim());
                    await CredentialStore.SaveCredentialAsync(credential);
                    _existingCredential = credential;
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

    private void OpenSetupWizard()
    {
        Navigation.NavigateTo("/setup");
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
        var credentialWasChanged = false;

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

            credentialWasChanged = !Equals(previousCredential, _existingCredential);

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
            string? rollbackError = null;
            if (credentialWasChanged)
            {
                try
                {
                    await RestoreCredentialAsync(previousCredential);
                }
                catch (Exception restoreException)
                {
                    rollbackError = $" The previous credential could not be restored: {restoreException.Message}";
                }
            }

            _credentialTestResult = $"Connection failed: {ex.Message}" +
                                    (credentialWasChanged && rollbackError is null
                                        ? " The previous credential was restored."
                                        : rollbackError);
            Trace.WriteLine($"[SonglistSpinner] API connection test failed: {ex}");
        }
        finally
        {
            _testingCredential = false;
        }
    }

    private async Task RestoreCredentialAsync(StreamerSongListCredential? credential)
    {
        if (credential is null)
            await CredentialStore.ClearCredentialAsync();
        else
            await CredentialStore.SaveCredentialAsync(credential);

        _existingCredential = credential;
        _hasCredential = credential is not null;
        _credentialKind = credential?.Kind ?? StreamerSongListCredentialKind.Streamer;
        _credentialClientId = credential?.ClientId ?? "";
        _credentialToken = "";
        _clearCredentialOnSave = false;
        _savedFormState = CaptureFormState();
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
            CaptureDisplayFields(_vm.WinnerDialogDisplayFields),
            _vm.PlayedListBgHex,
            _vm.PlayedListBgAlpha,
            _vm.UseIndependentNowPlayingBgAlpha,
            _vm.NowPlayingBgAlpha,
            _credentialKind,
            _credentialClientId,
            !string.IsNullOrWhiteSpace(_credentialToken),
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
        string WinnerDialogDisplayFields,
        string PlayedListBackground,
        double PlayedListBackgroundAlpha,
        bool UseIndependentNowPlayingBackgroundAlpha,
        double NowPlayingBackgroundAlpha,
        StreamerSongListCredentialKind CredentialKind,
        string CredentialClientId,
        bool CredentialTokenEdited,
        bool HasCredential,
        bool ClearCredentialOnSave);

    private enum SettingsSection
    {
        Connection,
        Spinner,
        Overlay,
        Appearance,
        Advanced
    }
}
