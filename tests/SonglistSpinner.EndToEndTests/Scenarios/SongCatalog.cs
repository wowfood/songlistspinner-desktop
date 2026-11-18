namespace SonglistSpinner.EndToEndTests.Scenarios;

/// <summary>
/// Readable songs the suites share, so expected values read the same in every test. Artist and title are unique
/// across the catalogue; the simulator gives every request of the same song the same song id.
/// </summary>
internal static class SongCatalog
{
    public static SongSeed Dreams { get; } = new("Fleetwood Mac", "Dreams", "night_owl");
    public static SongSeed GetLucky { get; } = new("Daft Punk", "Get Lucky", "early_bird");
    public static SongSeed TakeOnMe { get; } = new("a-ha", "Take On Me", "synth_lover", 3.50m);
    public static SongSeed DontStopMeNow { get; } = new("Queen", "Don't Stop Me Now", "regular_viewer");
    public static SongSeed MrBrightside { get; } = new("The Killers", "Mr. Brightside", "indie_kid");
    public static SongSeed Africa { get; } = new("Toto", "Africa", "long_time_fan", 10.00m);
    public static SongSeed DontStopBelievin { get; } = new("Journey", "Don't Stop Believin'", "karaoke_star");
    public static SongSeed DancingQueen { get; } = new("ABBA", "Dancing Queen", "disco_fan", 5.00m);
}
