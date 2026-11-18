using System.Xml.Linq;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <summary>
/// The version of the app under test, read from the Desktop project, so the tests follow a release's version bump
/// instead of naming one release.
/// </summary>
internal static class AppVersion
{
    private static readonly Lazy<string> Version = new(ReadDesktopVersion);

    /// <summary>The <c>VersionPrefix</c> the Desktop project builds the tested app with, such as 1.2.0.</summary>
    public static string Current => Version.Value;

    private static string ReadDesktopVersion()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "SonglistSpinner.Desktop.sln")))
            repository = repository.Parent;
        if (repository is null)
            throw new InvalidOperationException("The end-to-end tests must run from the repository checkout.");

        var project = XDocument.Load(Path.Combine(
            repository.FullName, "src", "SonglistSpinner.Desktop", "SonglistSpinner.Desktop.csproj"));
        return project.Descendants("VersionPrefix").Single().Value;
    }
}
