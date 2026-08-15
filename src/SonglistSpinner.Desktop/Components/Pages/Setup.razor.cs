using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;

namespace SonglistSpinner.Components.Pages;

public partial class Setup
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private VerificationState _apiState;
    private string _apiMessage = "Not checked yet";
    private bool _busy;
    private bool _canContinueWithWarnings;
    private string _clientId = "";
    private string? _completionMessage;
    private StreamerSongListCredential? _existingCredential;
    private StreamerSongListCredentialKind _credentialKind = StreamerSongListCredentialKind.Streamer;
    private string? _error;
    private VerificationState _eventsState;
    private string _eventsMessage = "Not checked yet";
    private string _fallbackPlatform = "twitch";
    private bool _hasExistingCredential;
    private int _historyCount;
    private StreamerSongListChannel? _matchedChannel;
    private VerificationState _overlayState;
    private string _overlayMessage = "Not checked yet";
    private int _queueCount;
    private StreamerSongListStreamer? _resolvedStreamer;
    private SettingsDto _settings = new();
    private int _step = 1;
    private string _streamerReference = "";
    private string _token = "";

    protected override async Task OnInitializedAsync()
    {
        _settings = LocalSettings.LoadSettings();
        _streamerReference = _settings.DefaultStreamerName;
        _fallbackPlatform = _settings.StreamerPlatform;

        try
        {
            _existingCredential = await CredentialStore.GetCredentialAsync(_lifetimeCts.Token);
            if (_existingCredential is not null)
            {
                _hasExistingCredential = true;
                _credentialKind = _existingCredential.Kind;
                _clientId = _existingCredential.ClientId ?? "";
            }
        }
        catch (Exception ex)
        {
            _error = $"Windows secure storage could not be read: {ex.Message}";
        }
    }

    private void ContinueToChannel()
    {
        _error = null;
        if (!_hasExistingCredential && string.IsNullOrWhiteSpace(_token))
        {
            _error = "Paste a StreamerSongList access token to continue.";
            return;
        }

        _step = 2;
    }

    private void BackToAccess()
    {
        if (_busy) return;
        _error = null;
        _step = 1;
    }

    private async Task VerifyConnectionAsync()
    {
        if (_busy) return;
        _error = null;
        _completionMessage = null;
        _canContinueWithWarnings = false;

        if (!StreamerSongListReferenceParser.TryParse(
                _streamerReference,
                _fallbackPlatform,
                out var channel,
                out var parseError))
        {
            _error = parseError;
            return;
        }

        var submittedToken = _token.Trim();
        var candidateCredential = string.IsNullOrWhiteSpace(submittedToken)
            ? _existingCredential
            : new StreamerSongListCredential(
                _credentialKind,
                submittedToken,
                string.IsNullOrWhiteSpace(_clientId) ? null : _clientId.Trim());
        if (candidateCredential is null)
        {
            _error = "A StreamerSongList access token is required.";
            return;
        }

        _busy = true;
        _matchedChannel = channel;
        _apiState = VerificationState.Running;
        _apiMessage = "Authenticating and loading the channel...";
        _eventsState = VerificationState.NotRun;
        _eventsMessage = "Waiting for the API check";
        _overlayState = VerificationState.NotRun;
        _overlayMessage = "Waiting for the API check";
        await InvokeAsync(StateHasChanged);

        var credentialWasReplaced = !string.IsNullOrWhiteSpace(submittedToken);
        try
        {
            if (credentialWasReplaced)
                await CredentialStore.SaveCredentialAsync(candidateCredential, _lifetimeCts.Token);

            _resolvedStreamer = await ApiService.ResolveStreamerAsync(channel, _lifetimeCts.Token);
            var queueTask = ApiService.FetchQueueAsync(channel, _lifetimeCts.Token);
            var historyTask = ApiService.FetchPlayHistoryAsync(
                channel,
                _settings.PlayHistoryPeriod,
                _lifetimeCts.Token);
            await Task.WhenAll(queueTask, historyTask);
            _queueCount = (await queueTask).Length;
            _historyCount = (await historyTask).Length;

            _settings.DefaultStreamerName = channel.Name;
            _settings.StreamerPlatform = channel.Platform;
            LocalSettings.SaveSettings(_settings);
            _existingCredential = candidateCredential;
            _hasExistingCredential = true;
            _token = "";
            _apiState = VerificationState.Passed;
            _apiMessage = $"Connected to channel #{_resolvedStreamer.Id} with {_queueCount} queued song(s).";
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            string? rollbackError = null;
            if (credentialWasReplaced)
            {
                try
                {
                    await RestoreCredentialAsync(_existingCredential);
                }
                catch (Exception restoreException)
                {
                    rollbackError = $" The previous credential could not be restored: {restoreException.Message}";
                }
            }

            _apiState = VerificationState.Failed;
            _apiMessage = "Connection failed";
            _error = ex.Message + rollbackError;
            _busy = false;
            return;
        }

        await InvokeAsync(StateHasChanged);
        await VerifyRealtimeAsync(_resolvedStreamer.Id);
        await InvokeAsync(StateHasChanged);
        await VerifyOverlayAsync();

        _busy = false;
        if (_eventsState == VerificationState.Passed && _overlayState == VerificationState.Passed)
        {
            _step = 3;
        }
        else
        {
            _canContinueWithWarnings = true;
            _error = "The channel is connected, but one supporting service could not be verified.";
        }
    }

    private async Task VerifyRealtimeAsync(int streamerId)
    {
        _eventsState = VerificationState.Running;
        _eventsMessage = "Connecting to StreamerSongList events...";
        await InvokeAsync(StateHasChanged);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string? lastError = null;
        try
        {
            await foreach (var notification in EventSource.SubscribeAsync(streamerId, timeout.Token))
            {
                if (notification.Kind == StreamerSongListEventKind.Connected)
                {
                    _eventsState = VerificationState.Passed;
                    _eventsMessage = "Realtime queue and history updates are available.";
                    return;
                }

                if (notification.Kind == StreamerSongListEventKind.Reconnecting)
                    lastError = notification.Error;
            }
        }
        catch (OperationCanceledException) when (!_lifetimeCts.IsCancellationRequested)
        {
            lastError ??= "The realtime connection timed out.";
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
        }

        _eventsState = VerificationState.Failed;
        _eventsMessage = lastError ?? "Realtime events could not be verified.";
    }

    private async Task VerifyOverlayAsync()
    {
        _overlayState = VerificationState.Running;
        _overlayMessage = "Checking the local overlay server...";
        await InvokeAsync(StateHasChanged);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await Http.GetAsync(
                OverlayService.OverlayUrl,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            response.EnsureSuccessStatusCode();
            _overlayState = VerificationState.Passed;
            _overlayMessage = $"Overlay available at {OverlayService.OverlayUrl}.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !_lifetimeCts.IsCancellationRequested)
        {
            _overlayState = VerificationState.Failed;
            _overlayMessage = $"Overlay check failed: {ex.Message}";
        }
    }

    private async Task RestoreCredentialAsync(StreamerSongListCredential? credential)
    {
        if (credential is null)
            await CredentialStore.ClearCredentialAsync(_lifetimeCts.Token);
        else
            await CredentialStore.SaveCredentialAsync(credential, _lifetimeCts.Token);
    }

    private void ContinueWithWarnings()
    {
        if (_apiState == VerificationState.Passed) _step = 3;
    }

    private async Task CopyOverlayUrlAsync()
    {
        await Clipboard.SetTextAsync(OverlayService.OverlayUrl);
        _completionMessage = "Overlay URL copied to the clipboard.";
    }

    private async Task OpenOverlayAsync()
    {
        try
        {
            await Launcher.Default.OpenAsync(new Uri(OverlayService.OverlayUrl));
            _completionMessage = "Overlay preview opened in your browser.";
        }
        catch (Exception ex)
        {
            _completionMessage = $"The overlay preview could not be opened: {ex.Message}";
        }
    }

    private void FinishSetup()
    {
        Navigation.NavigateTo("/dashboard");
    }

    private string StepClass(int step) => step == _step ? "active" : step < _step ? "complete" : "";

    private static string CheckClass(VerificationState state) => $"ss-setup-check {state.ToString().ToLowerInvariant()}";

    private static string CheckIcon(VerificationState state) => state switch
    {
        VerificationState.Running => "…",
        VerificationState.Passed => "✓",
        VerificationState.Failed => "!",
        _ => "○"
    };

    private static string PlatformLabel(string platform) => platform switch
    {
        "twitch" => "Twitch",
        "youtube" => "YouTube",
        "kick" => "Kick",
        "none" => "StreamerSongList",
        _ => platform
    };

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private enum VerificationState
    {
        NotRun,
        Running,
        Passed,
        Failed
    }
}
