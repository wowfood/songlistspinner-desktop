using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace SonglistSpinner.Services;

public static class WebViewBrowserArgumentsMauiAppBuilderExtensions
{
    /// <summary>
    /// TEST ONLY: starts the Blazor WebView's browser with <paramref name="arguments"/> (end-to-end tests pass
    /// <c>--remote-debugging-port</c>), keeping its data in <paramref name="userDataFolder"/>.
    /// </summary>
    /// <remarks>
    /// Neither documented route reaches the browser here (WebView2 runtime 154, Windows App SDK 1.8, MAUI 10): the
    /// WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS variable and the options set in BlazorWebView's
    /// BlazorWebViewInitializing event are both ignored, because the WebView2 control has already started with its
    /// default environment by the time MAUI's environment is ready. So the environment is created here, while the
    /// app starts, and handed to the control as the handler maps HostPage, which is before the control loads. If it
    /// is not ready by then, the browser starts without the arguments and a warning is logged.
    /// </remarks>
    public static MauiAppBuilder UseWebViewBrowserArguments(
        this MauiAppBuilder builder,
        string userDataFolder,
        string arguments)
    {
        var environment = CoreWebView2Environment.CreateWithOptionsAsync(
            browserExecutableFolder: null,
            userDataFolder,
            new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = arguments }).AsTask();

        BlazorWebViewHandler.BlazorWebViewMapper.PrependToMapping(nameof(BlazorWebView.HostPage), (handler, blazorWebView) =>
        {
            if (environment.IsCompletedSuccessfully)
            {
                _ = handler.PlatformView.EnsureCoreWebView2Async(environment.Result);
                return;
            }

            handler.MauiContext?.Services.GetService<ILogger<BlazorWebViewHandler>>()?.LogWarning(
                environment.Exception,
                "The WebView2 test environment was not ready; the browser starts without its test arguments");
        });
        return builder;
    }
}
