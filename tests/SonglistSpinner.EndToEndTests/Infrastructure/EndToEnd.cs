using Microsoft.Playwright;
using Xunit;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

internal static class EndToEnd
{
    public const string EnabledVariable = "SONGLISTSPINNER_E2E";

    /// <summary>
    /// How long a Playwright expectation retries before it fails. A spin animates for five seconds and the app then
    /// looks up the winner's queue position, so the default five seconds is too short.
    /// </summary>
    public const float ExpectTimeoutMilliseconds = 20_000;

    /// <summary>
    /// Skips the test unless <see cref="EnabledVariable"/> is 1, so the default test run and CI never open app
    /// windows. Call it first in every test, before anything is started.
    /// </summary>
    public static void SkipUnlessEnabled()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable(EnabledVariable) == "1",
            $"End-to-end tests open real app windows; set {EnabledVariable}=1 (scripts/run-e2e.ps1 does) to run them.");

        // Process-wide, so it is set where every test passes before it touches a page.
        Assertions.SetDefaultExpectTimeout(ExpectTimeoutMilliseconds);
    }
}
