using Microsoft.Playwright;
using SonglistSpinner.EndToEndTests.Infrastructure;
using Xunit;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class DashboardPage
{
    /// <summary>The Dashboard's layout root; it has the class <c>played-list-left</c> while the panel is on the left.</summary>
    public ILocator Container => Page.Locator("#container");

    /// <summary>The collapse button's arrow, which points towards the edge the played panel sits on.</summary>
    public ILocator CollapseIcon => Page.Locator("#collapseIcon");

    /// <summary>
    /// The separator that resizes the played panel; its <c>aria-valuenow</c> is the panel's width in pixels.
    /// </summary>
    public ILocator PlayedListResizeHandle =>
        Page.GetByRole(AriaRole.Separator, new() { Name = "Resize played songs panel", Exact = true });

    /// <summary>
    /// Waits until the theme sets the CSS custom property <paramref name="name"/> to exactly
    /// <paramref name="expected"/> (trimmed). The Dashboard applies its theme after it renders, so a single read
    /// could see the stylesheet's fallback; on timeout the failure names the value last read.
    /// </summary>
    public async Task ExpectThemeVariableAsync(string name, string expected)
    {
        try
        {
            await Page.WaitForFunctionAsync(
                "([name, expected]) => getComputedStyle(document.documentElement).getPropertyValue(name).trim() === expected",
                new[] { name, expected },
                new() { Timeout = EndToEnd.ExpectTimeoutMilliseconds });
        }
        catch (TimeoutException)
        {
            Assert.Equal(expected, await ReadThemeVariableAsync(name));
            throw;
        }
    }
}
