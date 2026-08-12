using MudBlazor.Utilities;
using SonglistSpinner.Core.Contracts;
using SonglistSpinner.Core.Data;
using SonglistSpinner.Extensions;

namespace SonglistSpinner.Components.Pages;

// Injected properties (LocalSettings, Config) come from @inject in Settings.razor.
public partial class Settings
{
    private readonly SettingsViewModel _vm = new();
    private string _credentialClientId = "";
    private StreamerSongListCredentialKind _credentialKind = StreamerSongListCredentialKind.Streamer;
    private string _credentialToken = "";
    private SettingsDto? _dto;
    private StreamerSongListCredential? _existingCredential;
    private bool _hasCredential;

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
    }

    private async Task Save()
    {
        _vm.SaveSuccess = false;
        _vm.SaveError = null;
        if (_dto == null) return;

        try
        {
            _vm.ApplyToDto(_dto);
            LocalSettings.SaveSettings(_dto);

            var token = string.IsNullOrWhiteSpace(_credentialToken)
                ? _existingCredential?.Token
                : _credentialToken.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                _existingCredential = new StreamerSongListCredential(
                    _credentialKind,
                    token,
                    string.IsNullOrWhiteSpace(_credentialClientId) ? null : _credentialClientId.Trim());
                await CredentialStore.SaveCredentialAsync(_existingCredential);
                _credentialToken = "";
                _hasCredential = true;
            }

            _vm.SaveSuccess = true;
        }
        catch (Exception ex)
        {
            _vm.SaveError = ex.Message;
        }
    }

    private async Task ClearApiCredential()
    {
        await CredentialStore.ClearCredentialAsync();
        _existingCredential = null;
        _credentialToken = "";
        _credentialClientId = "";
        _credentialKind = StreamerSongListCredentialKind.Streamer;
        _hasCredential = false;
    }
}
