using System.Runtime.CompilerServices;

// The desktop overlay server reports its health through OverlayStateService.SetServerHealth.
[assembly: InternalsVisibleTo("SonglistSpinner.Desktop")]
[assembly: InternalsVisibleTo("SonglistSpinner.Application.Tests")]
