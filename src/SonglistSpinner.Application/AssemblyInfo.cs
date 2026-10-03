using System.Runtime.CompilerServices;

// The desktop pages call the wheel script through the internal SpinnerInteropMethods names.
[assembly: InternalsVisibleTo("SonglistSpinner.Desktop")]
[assembly: InternalsVisibleTo("SonglistSpinner.Application.Tests")]
