using System.Globalization;
using SonglistSpinner.Core.Models;
using SonglistSpinner.Core.Services;
using Xunit;
using static SonglistSpinner.Core.Tests.TestSongs;

namespace SonglistSpinner.Core.Tests.Services;

public class SongDisplayTextTests
{
    // ── GetPrimaryRequester ───────────────────────────────────────────────

    [Fact]
    public void Given_QueueItemWithRequests_When_GetPrimaryRequester_Then_ReturnsFirstName()
    {
        Assert.Equal("User1", SongDisplayText.GetPrimaryRequester(Q(requester: "User1")));
    }

    [Fact]
    public void Given_QueueItemWithEmptyRequests_When_GetPrimaryRequester_Then_ReturnsUnknown()
    {
        var q = new SpinnerQueueItem { Song = new SpinnerSong(), Requests = [] };
        Assert.Equal("Unknown", SongDisplayText.GetPrimaryRequester(q));
    }

    [Fact]
    public void Given_QueueItemWithEmptyRequesterName_When_GetPrimaryRequester_Then_ReturnsUnknown()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests = [new SpinnerRequest { Name = "" }]
        };
        Assert.Equal("Unknown", SongDisplayText.GetPrimaryRequester(q));
    }

    [Fact]
    public void Given_QueueItemWithMultipleRequesters_When_GetPrimaryRequester_Then_ReturnsFirst()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests =
            [
                new SpinnerRequest { Name = "First" },
                new SpinnerRequest { Name = "Second" }
            ]
        };
        Assert.Equal("First", SongDisplayText.GetPrimaryRequester(q));
    }

    // ── BuildWheelLabel ───────────────────────────────────────────────────

    [Fact]
    public void Given_SongWithArtistTitleAndRequester_When_BuildWheelLabel_Then_ReturnsFormattedLabel()
    {
        Assert.Equal("Artist A - Song One (User1)", SongDisplayText.BuildWheelLabel(Q()));
    }

    [Fact]
    public void Given_SongWithEmptyArtist_When_BuildWheelLabel_Then_UsesUnknownForArtist()
    {
        Assert.Equal("Unknown - Song One (User1)", SongDisplayText.BuildWheelLabel(Q(artist: "")));
    }

    [Fact]
    public void Given_SongWithEmptyTitle_When_BuildWheelLabel_Then_UsesUnknownForTitle()
    {
        Assert.Equal("Artist A - Unknown (User1)", SongDisplayText.BuildWheelLabel(Q(title: "")));
    }

    [Fact]
    public void Given_SongWithNoRequests_When_BuildWheelLabel_Then_LabelContainsUnknownRequester()
    {
        var q = new SpinnerQueueItem { Song = new SpinnerSong { Artist = "A", Title = "T" }, Requests = [] };
        Assert.Equal("A - T (Unknown)", SongDisplayText.BuildWheelLabel(q));
    }

    // ── FormatDonation ────────────────────────────────────────────────────

    [Fact]
    public void Given_RequestWithDonationAmount_When_FormatDonation_Then_ReturnsDonationAmountValue()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests = [new SpinnerRequest { DonationAmount = 5.50m }]
        };
        Assert.Equal("5.50", SongDisplayText.FormatDonation(q));
    }

    [Fact]
    public void Given_CommaDecimalCulture_When_FormatDonation_Then_UsesInvariantDecimalPoint()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests = [new SpinnerRequest { DonationAmount = 5.50m }]
        };
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        string formatted;
        try
        {
            formatted = SongDisplayText.FormatDonation(q);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.Equal("5.50", formatted);
    }

    [Fact]
    public void Given_RequestWithNoDonationOrDonationAmountButHasAmount_When_FormatDonation_Then_ReturnsAmountValue()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests = [new SpinnerRequest { Amount = 2m }]
        };
        Assert.Equal("2", SongDisplayText.FormatDonation(q));
    }

    [Fact]
    public void Given_RequestWithNoDonationFields_When_FormatDonation_Then_ReturnsNone()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests = [new SpinnerRequest { Name = "User1" }]
        };
        Assert.Equal("None", SongDisplayText.FormatDonation(q));
    }

    [Fact]
    public void Given_QueueItemWithNoRequests_When_FormatDonation_Then_ReturnsNone()
    {
        var q = new SpinnerQueueItem { Song = new SpinnerSong(), Requests = [] };
        Assert.Equal("None", SongDisplayText.FormatDonation(q));
    }

    // ── GetFieldValue ─────────────────────────────────────────────────────

    [Fact]
    public void Given_ArtistField_When_GetFieldValue_Then_ReturnsArtistName()
    {
        Assert.Equal("Artist A", SongDisplayText.GetFieldValue(Q(), "artist"));
    }

    [Fact]
    public void Given_TitleField_When_GetFieldValue_Then_ReturnsSongTitle()
    {
        Assert.Equal("Song One", SongDisplayText.GetFieldValue(Q(), "title"));
    }

    [Fact]
    public void Given_RequesterField_When_GetFieldValue_Then_ReturnsPrimaryRequester()
    {
        Assert.Equal("User1", SongDisplayText.GetFieldValue(Q(), "requester"));
    }

    [Fact]
    public void Given_DonationField_When_GetFieldValue_Then_ReturnsFormattedDonation()
    {
        var q = new SpinnerQueueItem
        {
            Song = new SpinnerSong(),
            Requests = [new SpinnerRequest { DonationAmount = 10m }]
        };
        Assert.Equal("10", SongDisplayText.GetFieldValue(q, "donation"));
    }

    [Fact]
    public void Given_UnknownField_When_GetFieldValue_Then_ReturnsEmptyString()
    {
        Assert.Equal("", SongDisplayText.GetFieldValue(Q(), "foobar"));
    }

    [Fact]
    public void Given_FieldNameInUppercase_When_GetFieldValue_Then_ReturnsValue()
    {
        Assert.Equal("Artist A", SongDisplayText.GetFieldValue(Q(), "ARTIST"));
    }

    [Fact]
    public void Given_EmptyArtistValue_When_GetFieldValue_With_ArtistField_Then_ReturnsUnknown()
    {
        Assert.Equal("Unknown", SongDisplayText.GetFieldValue(Q(artist: ""), "artist"));
    }

    // ── CreateTextForFields ───────────────────────────────────────────────

    [Fact]
    public void Given_MultipleValidFields_When_CreateTextForFields_Then_JoinsPartsWithPipe()
    {
        var result = SongDisplayText.CreateTextForFields(Q(), ["artist", "title"]);
        Assert.Equal("Artist: Artist A | Title: Song One", result);
    }

    [Fact]
    public void Given_CustomSeparator_When_CreateTextForFields_Then_PreservesItExactly()
    {
        var result = SongDisplayText.CreateTextForFields(Q(), ["artist", "title"], " • ");
        Assert.Equal("Artist: Artist A • Title: Song One", result);
    }

    [Fact]
    public void Given_HiddenLabels_When_CreateTextForFields_Then_ReturnsValuesOnly()
    {
        var result = SongDisplayText.CreateTextForFields(
            Q(),
            ["artist", "title"],
            showLabels: false);
        Assert.Equal("Artist A | Song One", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_BlankSeparator_When_CreateTextForFields_Then_UsesDefault(string separator)
    {
        var result = SongDisplayText.CreateTextForFields(Q(), ["artist", "title"], separator);
        Assert.Equal("Artist: Artist A | Title: Song One", result);
    }

    [Fact]
    public void Given_FieldWithNoValue_When_CreateTextForFields_Then_SkipsField()
    {
        var result = SongDisplayText.CreateTextForFields(Q(), ["artist", "foobar"]);
        Assert.Equal("Artist: Artist A", result);
    }

    [Fact]
    public void Given_SingleField_When_CreateTextForFields_Then_CapitalizesFieldLabel()
    {
        var result = SongDisplayText.CreateTextForFields(Q(), ["requester"]);

        Assert.Equal("Requester: User1", result);
    }

    [Fact]
    public void Given_AllUnknownFields_When_CreateTextForFields_Then_ReturnsEmptyString()
    {
        var result = SongDisplayText.CreateTextForFields(Q(), ["unknown1", "unknown2"]);
        Assert.Equal("", result);
    }

    [Fact]
    public void Given_EmptyFieldList_When_CreateTextForFields_Then_ReturnsEmptyString()
    {
        var result = SongDisplayText.CreateTextForFields(Q(), []);
        Assert.Equal("", result);
    }

    // ── CreateNowPlayingText ──────────────────────────────────────────────

    [Fact]
    public void Given_NowPlayingFieldsSeparatorAndNoLabels_When_CreatingNowPlayingText_Then_UsesThatConfig()
    {
        var nowPlaying = new SpinnerNowPlayingConfig
        {
            Fields = [SongFieldNames.Title, SongFieldNames.Requester],
            Separator = " / ",
            ShowLabels = false
        };

        var text = SongDisplayText.CreateNowPlayingText(Q(), nowPlaying);

        Assert.Equal("Song One / User1", text);
    }

    [Fact]
    public void Given_NoNowPlayingFields_When_CreatingNowPlayingText_Then_ShowsTheDefaultFieldsWithLabels()
    {
        var nowPlaying = new SpinnerNowPlayingConfig { Fields = [] };

        var text = SongDisplayText.CreateNowPlayingText(Q(), nowPlaying);

        Assert.Equal("Artist: Artist A | Title: Song One", text);
    }
}
