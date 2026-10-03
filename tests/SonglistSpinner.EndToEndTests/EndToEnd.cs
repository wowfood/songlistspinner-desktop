using Xunit;

namespace SonglistSpinner.EndToEndTests;

internal static class EndToEnd
{
    public const string EnabledVariable = "SONGLISTSPINNER_E2E";

    /// <summary>
    /// Skips the test unless <see cref="EnabledVariable"/> is 1, so the default test run and CI never open app
    /// windows. Call it first in every test, before anything is started.
    /// </summary>
    public static void SkipUnlessEnabled() =>
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable(EnabledVariable) == "1",
            $"End-to-end tests open real app windows; set {EnabledVariable}=1 (scripts/run-e2e.ps1 does) to run them.");
}
