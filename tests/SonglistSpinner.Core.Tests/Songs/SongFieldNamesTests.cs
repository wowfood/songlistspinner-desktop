using SonglistSpinner.Core.Songs;
using Xunit;

namespace SonglistSpinner.Core.Tests.Songs;

public class SongFieldNamesTests
{
    [Fact]
    public void Given_SongFieldNames_When_ReadingValues_Then_WireTokensRemainStable()
    {
        Assert.Equal(["artist", "title", "requester", "donation"], SongFieldNames.Values);
    }

    [Fact]
    public void Given_SongFieldNames_When_ComparingValues_Then_EachValueIsCaseInsensitivelyUnique()
    {
        Assert.Distinct(SongFieldNames.Values, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_MixedFieldSelection_When_Normalizing_Then_OrderIsPreservedAndDuplicatesAreRemoved()
    {
        var result = SongFieldNames.NormalizeSelection(
            [" REQUESTER ", "unknown", "Title", "requester", "DONATION"]);

        Assert.Equal(["requester", "title", "donation"], result);
    }

    [Fact]
    public void Given_NoKnownFields_When_Normalizing_Then_UsesProvidedFallback()
    {
        var result = SongFieldNames.NormalizeSelection(
            ["unknown"],
            SongFieldNames.CreateWinnerDefaultSelection());

        Assert.Equal(["artist", "title", "requester"], result);
    }
}
