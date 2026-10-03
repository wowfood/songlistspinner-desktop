namespace SonglistSpinner.Core.Tests;

// Contract tests compare C# constants with the Desktop project's JavaScript, read from the source tree.
internal static class DesktopWebAssets
{
    public static string Read(string pathUnderWwwroot)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "SonglistSpinner.Desktop.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"No SonglistSpinner.Desktop.sln above {AppContext.BaseDirectory}; run the tests from the repository.");
        }

        return File.ReadAllText(
            Path.Combine(directory.FullName, "src", "SonglistSpinner.Desktop", "wwwroot", pathUnderWwwroot));
    }
}
