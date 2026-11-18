using System.Text.RegularExpressions;
using SonglistSpinner.EndToEndTests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests;

/// <summary>Settings &gt; Advanced: diagnostic output and the endpoints the running app uses, for troubleshooting.</summary>
public partial class AdvancedSettingsTests(SharedApp sharedApp) : IClassFixture<SharedApp>
{
    [Fact(Timeout = 180_000)]
    public async Task Given_TheAppOnTheSimulator_When_AdvancedIsOpened_Then_ItShowsTheVersionApiAndOverlayInUse()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        var settings = scenario.Settings;
        await settings.OpenAsync();

        await settings.Advanced.OpenAsync();

        await Expect(settings.Advanced.Endpoint("Application")).ToHaveTextAsync($"SonglistSpinner {AppVersion.Current}");
        await Expect(settings.Advanced.Endpoint("API")).ToHaveTextAsync(scenario.Simulator.ApiBaseAddress.ToString());
        await Expect(settings.Advanced.Endpoint("Local overlay"))
            .ToHaveTextAsync($"http://localhost:{scenario.App.OverlayPort}/overlay");
    }

    [Fact(Timeout = 180_000)]
    public async Task Given_DiagnosticOutputOff_When_ItIsEnabledAndSaved_Then_TheProfileLogRecordsThatLoggingStarted()
    {
        EndToEnd.SkipUnlessEnabled();
        var scenario = await sharedApp.BeginTestAsync(TestContext.Current.CancellationToken);
        // On a test profile the app keeps its logs in the profile, not under %LOCALAPPDATA%.
        var logPath = Path.Combine(scenario.Profile.Directory, "logs", "songlistspinner.log");
        var settings = scenario.Settings;
        await settings.OpenAsync();
        await settings.Advanced.OpenAsync();
        await Expect(settings.Advanced.DiagnosticOutput).Not.ToBeCheckedAsync();
        Assert.False(File.Exists(logPath), "The app wrote a diagnostic log before diagnostic output was enabled.");

        await settings.Advanced.DiagnosticOutput.CheckAsync();
        await settings.SaveAsync();

        // Saving opens the log before it reports "✓ Saved", and the log flushes every entry.
        Assert.Contains(
            ReadLogLines(logPath),
            line => DiagnosticLoggingEnabledEntry().IsMatch(line));
    }

    /// <summary>The log's lines, read while the app still holds it open for writing.</summary>
    private static List<string> ReadLogLines(string logPath)
    {
        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }

    [GeneratedRegex(@"^\S+ \[Information\] SonglistSpinner\.Services\.DiagnosticFileLoggerProvider: Diagnostic logging enabled\.$")]
    private static partial Regex DiagnosticLoggingEnabledEntry();
}
