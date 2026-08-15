namespace SonglistSpinner.Core.Models;

public sealed record ApplicationUpdateInfo(
    Version Version,
    string Tag,
    Uri ReleaseUri,
    DateTimeOffset? PublishedAt)
{
    public string DisplayVersion => Version.ToString(3);
}
