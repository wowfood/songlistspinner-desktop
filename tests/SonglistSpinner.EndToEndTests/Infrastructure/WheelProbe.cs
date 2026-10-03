using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// Records the labels the page gives its wheel. The wheel is drawn on a canvas, so its labels are in no element;
/// the probe wraps <c>SpinnerInterop.createWheel</c> (which the app and the overlay both draw through) and writes
/// the labels it receives, before any are shortened to fit, as JSON to <see cref="AttributeName"/> on the
/// document element, where a retrying Playwright expectation can read them. The app's own code is not changed.
/// </summary>
internal static class WheelProbe
{
    public const string AttributeName = "data-e2e-wheel-labels";

    // JSON.stringify escapes only quotes, backslashes and control characters; System.Text.Json's default encoder
    // also escapes apostrophes and non-ASCII letters, which song titles are full of.
    private static readonly JsonSerializerOptions ScriptJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Blazor and the overlay resolve SpinnerInterop.createWheel when they call it, so the replacement is used from
    // the next call on.
    private const string WrapFunction = """
        function wrapSpinnerInterop(interop) {
            if (!interop || interop.__e2eWheelProbe) return
            const createWheel = interop.createWheel
            interop.createWheel = function (items) {
                const labels = Array.isArray(items) ? items.map(item => item && item.label) : []
                document.documentElement.setAttribute('ATTRIBUTE', JSON.stringify(labels))
                return createWheel.apply(this, arguments)
            }
            interop.__e2eWheelProbe = true
        }
        """;

    /// <summary>
    /// Wraps the wheel of the app's page. Blazor caches each JavaScript function it has called, so wrapping after
    /// the app drew its first wheel would never be seen: the probe is registered for every load and, when the page
    /// had already loaded without it, the page is reloaded (at launch, before any test acts).
    /// </summary>
    public static async Task InstallInAppAsync(IPage page, Func<Task> waitForApp)
    {
        await InstallBeforeLoadAsync(page.Context);
        if (await IsInstalledAsync(page)) return;

        await page.ReloadAsync();
        await waitForApp();
        if (!await IsInstalledAsync(page))
            throw new InvalidOperationException("The wheel probe was not installed after reloading the app's page.");
    }

    /// <summary>
    /// Wraps the wheel of every page the context loads from now on, as soon as its script defines
    /// <c>window.SpinnerInterop</c>, so the first wheel the page draws is recorded too.
    /// </summary>
    public static Task InstallBeforeLoadAsync(IBrowserContext context) =>
        context.AddInitScriptAsync($$"""
            (() => {
                {{Script}}
                let interop
                Object.defineProperty(window, 'SpinnerInterop', {
                    configurable: true,
                    get() { return interop },
                    set(value) { interop = value; wrapSpinnerInterop(value) }
                })
            })()
            """);

    /// <summary>Waits until the page's wheel was last drawn with exactly <paramref name="labels"/>, in order.</summary>
    public static Task ExpectLabelsAsync(IPage page, IReadOnlyList<string> labels) =>
        Assertions.Expect(page.Locator("html"))
            .ToHaveAttributeAsync(AttributeName, JsonSerializer.Serialize(labels, ScriptJson));

    private static async Task<bool> IsInstalledAsync(IPage page)
    {
        await page.WaitForFunctionAsync("() => Boolean(window.SpinnerInterop)");
        return await page.EvaluateAsync<bool>("() => window.SpinnerInterop.__e2eWheelProbe === true");
    }

    private static string Script => WrapFunction.Replace("ATTRIBUTE", AttributeName, StringComparison.Ordinal);
}
