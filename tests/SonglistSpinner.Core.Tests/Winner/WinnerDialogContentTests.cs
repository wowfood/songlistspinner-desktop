using SonglistSpinner.Core.Settings;
using SonglistSpinner.Core.Winner;
using Xunit;
using static SonglistSpinner.Core.Tests.TestSongs;

namespace SonglistSpinner.Core.Tests.Winner;

public class WinnerDialogContentTests
{
    // ── SelectFieldNames ──────────────────────────────────────────────────

    [Fact]
    public void Given_ConfiguredWinnerFieldsWithoutRequester_When_SelectFieldNames_Then_DoesNotAddRequester()
    {
        var fields = WinnerDialogContent.SelectFieldNames(Cfg(winnerFields: ["artist", "title"]));

        Assert.Equal(["artist", "title"], fields);
    }

    [Fact]
    public void Given_ConfigWithRequesterAlreadyPresent_When_SelectFieldNames_Then_DoesNotDuplicateRequester()
    {
        var fields = WinnerDialogContent.SelectFieldNames(
            Cfg(winnerFields: ["artist", "requester", "REQUESTER"]));
        Assert.Single(fields, f => f == "requester");
    }

    [Fact]
    public void Given_ConfigWithMultipleFields_When_SelectFieldNames_Then_PreservesAllOriginalFields()
    {
        var fields = WinnerDialogContent.SelectFieldNames(
            Cfg(winnerFields: ["donation", "title", "artist"]));

        Assert.Equal(["donation", "title", "artist"], fields);
    }

    [Fact]
    public void Given_ConfigWithEmptyFields_When_SelectFieldNames_Then_DefaultsToArtistTitleAndRequester()
    {
        var cfg = new SpinnerConfig { WinnerDialog = new SpinnerWinnerDialogConfig { Fields = [] } };

        var fields = WinnerDialogContent.SelectFieldNames(cfg);

        Assert.Equal(["artist", "title", "requester"], fields);
    }

    [Fact]
    public void Given_FieldsInMixedCase_When_SelectFieldNames_Then_ReturnsLowercaseFields()
    {
        var fields = WinnerDialogContent.SelectFieldNames(Cfg(winnerFields: ["ARTIST", "Title"]));

        Assert.Equal(["artist", "title"], fields);
    }

    // ── CreateFields ──────────────────────────────────────────────────────

    [Fact]
    public void Given_ArtistAndTitleAreNotSelected_When_CreateFields_Then_OmitsBothFields()
    {
        var result = WinnerDialogContent.CreateFields(
            Q(requester: "Singer42"),
            Cfg(winnerFields: ["requester"]));

        var field = Assert.Single(result);
        Assert.Equal("Requester", field.Label);
        Assert.Equal("Singer42", field.Value);
    }

    [Fact]
    public void Given_OrderedWinnerFields_When_CreateFields_Then_PreservesLabelValueOrder()
    {
        var result = WinnerDialogContent.CreateFields(
            Q(artist: "Band", title: "Track", requester: "Singer42"),
            Cfg(winnerFields: ["title", "requester", "artist"]));

        Assert.Equal(
            [
                new WinnerDialogField("Title", "Track"),
                new WinnerDialogField("Requester", "Singer42"),
                new WinnerDialogField("Artist", "Band")
            ],
            result);
    }

    // ── FindQueuePosition ─────────────────────────────────────────────────

    [Fact]
    public void Given_MatchingQueueEntryWithPositivePosition_When_FindQueuePosition_Then_ReturnsPosition()
    {
        var queue = new[] { Q(queueId: 91, position: 4), Q(queueId: 92, position: 5) };

        var result = WinnerDialogContent.FindQueuePosition(queue, 91);

        Assert.Equal(4, result);
    }

    [Fact]
    public void Given_QueueEntryIsMissing_When_FindQueuePosition_Then_ReturnsNull()
    {
        var result = WinnerDialogContent.FindQueuePosition([Q(queueId: 91, position: 4)], 92);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_MatchingQueueEntryWithInvalidPosition_When_FindQueuePosition_Then_ReturnsNull(int position)
    {
        var result = WinnerDialogContent.FindQueuePosition([Q(queueId: 91, position: position)], 91);

        Assert.Null(result);
    }
}
