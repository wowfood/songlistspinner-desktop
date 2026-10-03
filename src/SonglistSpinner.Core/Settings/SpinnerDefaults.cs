using System.Diagnostics.CodeAnalysis;

namespace SonglistSpinner.Core.Settings;

// The values a user with no saved settings gets. SettingsDto (the persisted form) and the Spinner*Config
// models (what the wheel and the overlay receive) both take their initial values from here, so the two
// can't drift. Changing a value here changes what new users see.
public static class SpinnerDefaults
{
    public const string FontFamily = "sans-serif";

    // Returns a new array on every call: configs hand WheelColors to callers as a mutable array.
    public static string[] CreateWheelColors() =>
    [
        "#ff6b6b", "#4ecdc4", "#45b7d1", "#f9ca24",
        "#6c5ce7", "#a29bfe", "#fd79a8", "#fdcb6e"
    ];

    public static class Background
    {
        public const string Color = "#111111";
    }

    public static class Streamer
    {
        public const bool HideChangeOptionWhenDefault = true;
    }

    public static class PlayedList
    {
        public const string FontSize = "0.875rem";
        public const int MaxLines = 2;
        public const bool ShowLabels = true;
    }

    public static class NowPlaying
    {
        public const string FontSize = "1.125rem";
        public const string Width = "28rem";
        public const bool ShowLabels = true;
    }

    public static class WinnerDialog
    {
        public const string FontSize = "1rem";
        public const string Width = "36rem";
        public const bool ShowQueuePosition = true;
    }

    public static class Colors
    {
        public const string Text = "#ffffff";
        public const string PanelBackground = "rgba(0, 0, 0, 0.7)";
        public const string PlayedItemBackground = "#222222";
        public const string ResizeHandleBackground = "#333333";
        public const string ResizeHandleHoverBackground = "#555555";
        public const string ToggleBackground = "#222222";
        public const string ButtonBackground = "#555555";
        public const string ButtonText = "#CCCCCC";

        [SuppressMessage("Naming", "CA1720:Identifier contains type name",
            Justification = "The wheel's pointer is the domain name for this colour.")]
        public const string Pointer = "wheat";
    }
}
