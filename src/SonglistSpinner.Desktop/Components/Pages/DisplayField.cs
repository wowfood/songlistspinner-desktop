namespace SonglistSpinner.Components.Pages;

/// <summary>A song field in one of the Settings field-order editors, and whether it is shown.</summary>
public sealed class DisplayField
{
    public string Name { get; set; } = "";
    public bool Selected { get; set; }
}
