using SonglistSpinner.Core.Settings;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class SettingsViewModelTests
{
    [Fact]
    public void Given_AnEditedDraft_When_ApplyingItToTheSettings_Then_TheOverlayReceivesWhatTheEditorShows()
    {
        var settings = new SettingsDto();
        var viewModel = Loaded(settings);
        viewModel.WheelColorsRaw = "#ff0000\n\n  #00ff00  \n";
        viewModel.ToggleField("requester");
        viewModel.MoveField("requester", 0);
        viewModel.ToggleNowPlayingField("title");
        viewModel.ToggleWinnerDialogField("donation");
        viewModel.MoveWinnerDialogField("donation", 0);
        viewModel.PlayedListBgHex = "#336699";
        viewModel.PlayedListBgAlpha = 0.5;
        viewModel.NowPlayingBgAlpha = 0.9;

        viewModel.ApplyToDto(settings);
        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(["#ff0000", "#00ff00"], config.WheelColors);
        Assert.Equal(["requester", "artist", "title"], config.PlayedList.Fields);
        Assert.Equal(["artist"], config.NowPlaying.Fields);
        Assert.Equal(["donation", "artist", "title", "requester"], config.WinnerDialog.Fields);
        Assert.Equal("rgba(51,102,153,0.50)", config.Colors.PlayedListBackground);
        // Without its own opacity, Now Playing follows the played list's, whatever the hidden slider says.
        Assert.Null(settings.NowPlayingBackgroundOpacity);
        Assert.Equal("rgba(51,102,153,0.50)", config.Colors.NowPlayingBackground);
    }

    [Fact]
    public void Given_NowPlayingHasItsOwnOpacity_When_ApplyingItToTheSettings_Then_NowPlayingUsesThatOpacity()
    {
        var settings = new SettingsDto();
        var viewModel = Loaded(settings);
        viewModel.PlayedListBgHex = "#336699";
        viewModel.PlayedListBgAlpha = 0.5;
        viewModel.UseIndependentNowPlayingBgAlpha = true;
        viewModel.NowPlayingBgAlpha = 0.25;

        viewModel.ApplyToDto(settings);
        var config = SettingsDtoConverter.ToSpinnerConfig(settings);

        Assert.Equal(0.25, settings.NowPlayingBackgroundOpacity);
        Assert.Equal("rgba(51,102,153,0.25)", config.Colors.NowPlayingBackground);
    }

    [Fact]
    public void Given_UnreadableWheelColors_When_Loading_Then_TheEditorShowsTheDefaultPalette()
    {
        var viewModel = Loaded(new SettingsDto { WheelColors = "not json" });

        Assert.Equal(string.Join("\n", SpinnerDefaults.CreateWheelColors()), viewModel.WheelColorsRaw);
    }

    [Fact]
    public void Given_SettingsSavedBeforeWinnerFieldsExisted_When_Loading_Then_WinnerFieldsAreThePlayedFieldsPlusRequester()
    {
        var viewModel = Loaded(new SettingsDto { PlayedListFields = """["title","donation"]""", WinnerDialogFields = null });

        Assert.Equal(
            ["title", "donation", "requester"],
            viewModel.WinnerDialogDisplayFields.Where(field => field.Selected).Select(field => field.Name));
    }

    [Fact]
    public void Given_ANamedPointerColor_When_Loading_Then_TheColorPickerGetsItsHex()
    {
        var settings = new SettingsDto { ColorPointer = "red" };

        Loaded(settings);

        Assert.Equal("#ff0000", settings.ColorPointer);
    }

    [Fact]
    public void Given_PlayedListFields_When_MovingAFieldNamedInAnyCase_Then_ItTakesTheTargetPosition()
    {
        var viewModel = Loaded(new SettingsDto());

        var moved = viewModel.MoveField("TITLE", 0);

        Assert.True(moved);
        Assert.Equal(["title", "artist", "requester", "donation"], FieldNames(viewModel.DisplayFields));
    }

    [Fact]
    public void Given_ATargetPastTheEnd_When_MovingAField_Then_ItMovesToTheLastPosition()
    {
        var viewModel = Loaded(new SettingsDto());

        var moved = viewModel.MoveField("artist", 99);

        Assert.True(moved);
        Assert.Equal(["title", "requester", "donation", "artist"], FieldNames(viewModel.DisplayFields));
    }

    [Theory]
    [InlineData("artist", 0)]
    [InlineData("genre", 1)]
    public void Given_AFieldAlreadyThereOrUnknown_When_MovingIt_Then_NothingChanges(string fieldName, int targetIndex)
    {
        var viewModel = Loaded(new SettingsDto());

        var moved = viewModel.MoveField(fieldName, targetIndex);

        Assert.False(moved);
        Assert.Equal(["artist", "title", "requester", "donation"], FieldNames(viewModel.DisplayFields));
    }

    [Fact]
    public void Given_ASelectedField_When_ToggledByNameInAnyCase_Then_ItIsExcluded()
    {
        var viewModel = Loaded(new SettingsDto());

        var toggled = viewModel.ToggleField("ARTIST");

        Assert.True(toggled);
        Assert.False(viewModel.DisplayFields.Single(field => field.Name == "artist").Selected);
    }

    [Fact]
    public void Given_AnUnknownField_When_Toggled_Then_NothingChanges()
    {
        var viewModel = Loaded(new SettingsDto());

        var toggled = viewModel.ToggleField("genre");

        Assert.False(toggled);
        Assert.Equal(["artist", "title"], viewModel.DisplayFields.Where(field => field.Selected).Select(field => field.Name));
    }

    private static SettingsViewModel Loaded(SettingsDto settings)
    {
        var viewModel = new SettingsViewModel();
        viewModel.Initialize(settings);
        return viewModel;
    }

    private static IEnumerable<string> FieldNames(IEnumerable<DisplayField> fields) => fields.Select(field => field.Name);
}
