using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>The overlay layout the Dashboard and Settings drive: inline styles and the collapsed list.</summary>
internal sealed partial class OverlayPage
{
    /// <summary>Waits until the played list is hidden as collapsed, as the Dashboard's collapse button asks.</summary>
    public Task ExpectPlayedListCollapsedAsync() => Expect(PlayedList.Root).ToHaveClassAsync(CollapsedClass());

    public Task ExpectPlayedListExpandedAsync() => Expect(PlayedList.Root).Not.ToHaveClassAsync(CollapsedClass());

    /// <summary>
    /// Waits until <paramref name="element"/>'s inline style sets <paramref name="property"/> to exactly
    /// <paramref name="value"/>, as the overlay script writes it (for example <c>width</c> and <c>32rem</c>).
    /// </summary>
    public static Task ExpectInlineStyleAsync(ILocator element, string property, string value) =>
        Expect(element).ToHaveAttributeAsync(
            "style",
            new Regex($@"(^|;)\s*{Regex.Escape(property)}:\s*{Regex.Escape(value)}\s*(;|$)"));

    [GeneratedRegex(@"(^|\s)collapsed(\s|$)")]
    private static partial Regex CollapsedClass();
}
