using SonglistSpinner.Core.Api.V2;
using Xunit;

namespace SonglistSpinner.Core.Tests.Api.V2;

public class StreamerSongListEventTypesTests
{
    [Fact]
    public void Given_QueueChanges_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(
            [
                "now_playing_update", "queue_add", "queue_clear", "queue_remove", "queue_reorder", "queue_update"
            ],
            StreamerSongListEventTypes.QueueChanges);
    }

    [Fact]
    public void Given_PlayHistoryChanges_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(
            ["play_history_add", "play_history_remove"],
            StreamerSongListEventTypes.PlayHistoryChanges);
    }

    [Fact]
    public void Given_AllEventTypes_When_ComparingValues_Then_EachValueIsCaseInsensitivelyUnique()
    {
        Assert.Distinct(
            StreamerSongListEventTypes.QueueChanges.Concat(StreamerSongListEventTypes.PlayHistoryChanges),
            StringComparer.OrdinalIgnoreCase);
    }
}
