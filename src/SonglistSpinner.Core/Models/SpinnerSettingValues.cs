namespace SonglistSpinner.Core.Models;

public static class SpinnerSettingValues
{
    public static class BackgroundModes
    {
        public const string Color = "color";
        public const string Transparent = "transparent";
        public const string LegacyTransparent = "transparant";
        public const string Default = Color;

        private static readonly string[] SupportedValues = [Color, Transparent];

        public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

        public static bool TryNormalize(string? value, out string normalized)
        {
            if (string.Equals(value?.Trim(), LegacyTransparent, StringComparison.OrdinalIgnoreCase))
            {
                normalized = Transparent;
                return true;
            }

            return SpinnerSettingValues.TryNormalize(value, SupportedValues, out normalized);
        }

        public static string NormalizeOrDefault(string? value) =>
            TryNormalize(value, out var normalized) ? normalized : Default;
    }

    public static class PlayedListPositions
    {
        public const string Left = "left";
        public const string Right = "right";
        public const string Default = Right;

        private static readonly string[] SupportedValues = [Right, Left];

        public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

        public static bool TryNormalize(string? value, out string normalized) =>
            SpinnerSettingValues.TryNormalize(value, SupportedValues, out normalized);

        public static string NormalizeOrDefault(string? value) =>
            TryNormalize(value, out var normalized) ? normalized : Default;
    }

    public static class NowPlayingPositions
    {
        public const string TopLeft = "top-left";
        public const string TopCenter = "top-center";
        public const string TopRight = "top-right";
        public const string BottomLeft = "bottom-left";
        public const string BottomCenter = "bottom-center";
        public const string BottomRight = "bottom-right";
        public const string Default = BottomLeft;

        private static readonly string[] SupportedValues =
        [
            TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight
        ];

        public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

        public static bool TryNormalize(string? value, out string normalized) =>
            SpinnerSettingValues.TryNormalize(value, SupportedValues, out normalized);

        public static string NormalizeOrDefault(string? value) =>
            TryNormalize(value, out var normalized) ? normalized : Default;
    }

    public static class PlayedListNumberingStarts
    {
        public const string Top = "top";
        public const string Bottom = "bottom";
        public const string Default = Bottom;

        private static readonly string[] SupportedValues = [Top, Bottom];

        public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

        public static bool TryNormalize(string? value, out string normalized) =>
            SpinnerSettingValues.TryNormalize(value, SupportedValues, out normalized);

        public static string NormalizeOrDefault(string? value) =>
            TryNormalize(value, out var normalized) ? normalized : Default;
    }

    public static class PlayHistoryPeriods
    {
        public const string Stream = "stream";
        public const string Day = "day";
        public const string Week = "week";
        public const string Month = "month";
        public const string All = "all";
        public const string Default = Week;

        private static readonly string[] SupportedValues = [Stream, Day, Week, Month, All];

        public static IReadOnlyList<string> Values { get; } = Array.AsReadOnly(SupportedValues);

        public static bool TryNormalize(string? value, out string normalized) =>
            SpinnerSettingValues.TryNormalize(value, SupportedValues, out normalized);

        public static string NormalizeOrDefault(string? value) =>
            TryNormalize(value, out var normalized) ? normalized : Default;
    }

    private static bool TryNormalize(
        string? value,
        IEnumerable<string> supportedValues,
        out string normalized)
    {
        var candidate = value?.Trim();
        foreach (var supported in supportedValues)
        {
            if (!string.Equals(candidate, supported, StringComparison.OrdinalIgnoreCase)) continue;
            normalized = supported;
            return true;
        }

        normalized = "";
        return false;
    }
}
