using SonglistSpinner.Simulator;
using Xunit;

namespace SonglistSpinner.IntegrationTests.Simulator;

/// <summary>The simulator's own play-history paging, which the client's period and page-size tests rely on.</summary>
public class PlayHistoryQueryTests
{
    private static readonly DateTimeOffset PlayedAfter = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_PlaysEitherSideOfPlayedAfter_When_Applying_Then_KeepsOnlyLaterPlaysNewestFirst()
    {
        var history = new[]
        {
            Played("Before", PlayedAfter.AddTicks(-1)),
            Played("Later", PlayedAfter.AddHours(1)),
            Played("AtTheBoundary", PlayedAfter),
            Played("Latest", PlayedAfter.AddHours(2))
        };

        var (page, total) = new PlayHistoryQuery(PlayedAfter, NewestFirst: true, Limit: 100).Apply(history);

        Assert.Equal(["Latest", "Later"], page.Select(song => song.Title));
        Assert.Equal(2, total);
    }

    [Fact]
    public void Given_MoreMatchesThanTheLimit_When_ApplyingOldestFirst_Then_ReturnsTheOldestPageAndCountsEveryMatch()
    {
        var history = new[]
        {
            Played("Third", PlayedAfter.AddHours(3)),
            Played("First", PlayedAfter.AddHours(1)),
            Played("Second", PlayedAfter.AddHours(2))
        };

        var (page, total) = new PlayHistoryQuery(null, NewestFirst: false, Limit: 2).Apply(history);

        Assert.Equal(["First", "Second"], page.Select(song => song.Title));
        Assert.Equal(3, total);
    }

    private static SimulatedPlayedSong Played(string title, DateTimeOffset playedAt) =>
        new(1, "Artist", title, 1, playedAt, []);
}
