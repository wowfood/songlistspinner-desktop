using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// The played-songs list, which the Dashboard and the overlay render with the same markup. Without a header row
/// each song is one line of text ("1. Artist: Daft Punk | Title: Get Lucky"); with one, the songs are rows of
/// cells under a header row ("#", "Artist", "Title").
/// </summary>
/// <remarks>
/// Text expectations normalise whitespace (runs collapse to one space, ends are trimmed), so a separator's exact
/// spacing is asserted through <see cref="ExpectTableSeparatorAsync"/>, which reads the separators' title.
/// </remarks>
internal sealed partial class PlayedListPanel
{
    private readonly Func<string, ILocator> _locate;

    /// <summary>The list on the Dashboard or on an overlay page.</summary>
    public PlayedListPanel(IPage page) : this(selector => page.Locator(selector))
    {
    }

    /// <summary>The list in a frame, such as the Settings page's overlay preview.</summary>
    public PlayedListPanel(IFrameLocator frame) : this(selector => frame.Locator(selector))
    {
    }

    private PlayedListPanel(Func<string, ILocator> locate) => _locate = locate;

    public ILocator Root => _locate("#playedList");

    public ILocator Items => _locate("#playedSongsUl > li");

    /// <summary>"No played songs in the selected history period." when the period has none (Dashboard only).</summary>
    public ILocator EmptyState => _locate("#playedSongsUl > li.played-list-empty-state");

    public ILocator HeaderRow => _locate("#playedSongsUl > li.played-field-header-row");

    private ILocator TextLines => _locate("#playedSongsUl > li > .played-song-text");

    private ILocator TableRows => _locate("#playedSongsUl > li.played-field-grid:not(.played-field-header-row)");

    /// <summary>Waits until the list shows exactly <paramref name="lines"/>, newest song first.</summary>
    public async Task ExpectLinesAsync(params string[] lines)
    {
        await Expect(TextLines).ToHaveTextAsync(lines);
        await Expect(Items).ToHaveCountAsync(lines.Length);
    }

    /// <summary>
    /// The lines as rendered, with only the markup's surrounding whitespace trimmed, for asserting spacing the
    /// retrying expectations normalise away. Read after an expectation has shown the list is current.
    /// </summary>
    public async Task<string[]> ReadLinesAsync() =>
        [.. (await TextLines.AllTextContentsAsync()).Select(line => line.Trim())];

    /// <summary>
    /// Waits until the header row shows exactly <paramref name="headers"/>: "#" first when numbers are shown,
    /// then one header per field.
    /// </summary>
    public Task ExpectHeadersAsync(params string[] headers) =>
        Expect(HeaderRow.Locator(":scope > span:not(.played-field-separator)")).ToHaveTextAsync(headers);

    /// <summary>
    /// Waits until the table has exactly <paramref name="rows"/>, each given as its cells: the number ("1.") when
    /// numbers are shown, then one value per field ("" for a played song without a donation).
    /// </summary>
    public async Task ExpectTableRowsAsync(params string[][] rows)
    {
        await Expect(TableRows).ToHaveCountAsync(rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            await Expect(TableRows.Nth(index).Locator(":scope > span:not(.played-field-separator)"))
                .ToHaveTextAsync(rows[index]);
        }
    }

    /// <summary>Waits until every separator between table cells is exactly <paramref name="separator"/>.</summary>
    public async Task ExpectTableSeparatorAsync(string separator)
    {
        var separators = _locate("#playedSongsUl .played-field-separator");
        await Expect(separators.First).ToBeAttachedAsync();
        foreach (var element in await separators.AllAsync())
            await Expect(element).ToHaveAttributeAsync("title", separator);
    }

    /// <summary>
    /// The value the list's inline style gives <paramref name="property"/>, such as <c>width</c>, as the Dashboard's
    /// resize handle writes it; empty when the style does not set it.
    /// </summary>
    public Task<string> ReadInlineStyleAsync(string property) =>
        Root.EvaluateAsync<string>("(list, property) => list.style.getPropertyValue(property)", property);

    /// <summary>The list's rendered width in CSS pixels.</summary>
    public Task<double> ReadRenderedWidthAsync() =>
        Root.EvaluateAsync<double>("list => list.getBoundingClientRect().width");
}
