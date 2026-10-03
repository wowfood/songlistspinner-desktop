using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Playwright;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// Records the labels a page gives its wheel. The wheel is drawn on a canvas, so its labels are in no element; the
/// probe writes the labels passed to <c>SpinnerInterop.createWheel</c> (which the app and the overlay both draw
/// through), before the wheel shortens any to fit, as JSON to <see cref="AttributeName"/> on the document
/// element, where a retrying Playwright expectation can read them. Neither page's code is changed.
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

    // Blazor WebView sends each JavaScript call from .NET as a WebView2 web message,
    // __bwv:["BeginInvokeJS",callId,"identifier","[args as JSON]",...]; every message listener receives it.
    // Wrapping SpinnerInterop.createWheel instead would miss the app's calls, because Blazor caches the function
    // it first resolved for an identifier, and the app has drawn its first wheel before the DevTools connection
    // opens. This relies on that message format, an implementation detail of Blazor WebView (seen on MAUI 10).
    private const string AppTap = """
        () => {
            if (window.__e2eWheelProbe) return
            window.__e2eWheelProbe = true
            window.chrome.webview.addEventListener('message', event => {
                const data = event.data
                if (typeof data !== 'string' || !data.startsWith('__bwv:')) return
                let message
                try { message = JSON.parse(data.slice('__bwv:'.length)) } catch { return }
                if (message[0] !== 'BeginInvokeJS' || message[2] !== 'SpinnerInterop.createWheel') return
                const items = JSON.parse(message[3])[0]
                const labels = Array.isArray(items) ? items.map(item => item && item.label) : []
                document.documentElement.setAttribute('ATTRIBUTE', JSON.stringify(labels))
            })
        }
        """;

    // The overlay calls SpinnerInterop.createWheel from its own script, which looks the function up on each call.
    private const string OverlayWrap = """
        (() => {
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
            let interop
            Object.defineProperty(window, 'SpinnerInterop', {
                configurable: true,
                get() { return interop },
                set(value) { interop = value; wrapSpinnerInterop(value) }
            })
        })()
        """;

    /// <summary>Records the app's wheel from now on: the next channel load, refresh or spin redraws it.</summary>
    public static Task InstallInAppAsync(IPage page) => page.EvaluateAsync(WithAttribute(AppTap));

    /// <summary>
    /// Records the wheel of every page the context loads from now on, from the first wheel the page draws. Use it
    /// for the overlay page, before navigating to it.
    /// </summary>
    public static Task InstallBeforeLoadAsync(IBrowserContext context) =>
        context.AddInitScriptAsync(WithAttribute(OverlayWrap));

    /// <summary>Waits until the page's wheel was last drawn with exactly <paramref name="labels"/>, in order.</summary>
    public static Task ExpectLabelsAsync(IPage page, IReadOnlyList<string> labels) =>
        Assertions.Expect(page.Locator("html"))
            .ToHaveAttributeAsync(AttributeName, JsonSerializer.Serialize(labels, ScriptJson));

    private static string WithAttribute(string script) =>
        script.Replace("ATTRIBUTE", AttributeName, StringComparison.Ordinal);
}
