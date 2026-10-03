using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

/// <summary>
/// One display-field editor: a chip per field ("Artist", "Song title", "Requester", "Donation") that is selected
/// while the field is shown, with buttons to move it earlier or later. Field names are the stored values
/// (<c>artist</c>, <c>title</c>, <c>requester</c>, <c>donation</c>).
/// </summary>
internal sealed partial class FieldOrderEditor(ILocator root)
{
    private ILocator Chips => root.Locator(".ss-chip");

    /// <summary>Waits until the chips are in exactly this order, by stored field name.</summary>
    public async Task ExpectOrderAsync(params string[] fieldNames)
    {
        await Expect(Chips).ToHaveCountAsync(fieldNames.Length);
        for (var index = 0; index < fieldNames.Length; index++)
            await Expect(Chips.Nth(index)).ToHaveAttributeAsync("data-field-name", fieldNames[index]);
    }

    /// <summary>Selects or clears <paramref name="label"/> (the chip's text, such as "Requester").</summary>
    /// <remarks>
    /// Selection is read from the chip's <c>ss-chip-selected</c> class: the toggle's <c>aria-pressed</c> renders
    /// as "" when pressed and is left out otherwise, so it does not carry the state.
    /// </remarks>
    public async Task SetSelectedAsync(string label, bool selected)
    {
        var chip = Chip(label);
        var isSelected = (await chip.GetAttributeAsync("class") ?? "").Split(' ').Contains("ss-chip-selected");
        if (isSelected != selected) await Toggle(label).ClickAsync();
        await Expect(chip).ToHaveClassAsync(new Regex(selected ? @"\bss-chip-selected\b" : @"\bss-chip-unselected\b"));
    }

    /// <summary>The chip of <paramref name="label"/>; its class is <c>ss-chip-selected</c> while the field is shown.</summary>
    public ILocator Chip(string label) => Chips.Filter(new() { Has = Toggle(label) });

    public Task MoveEarlierAsync(string label) =>
        root.GetByRole(AriaRole.Button, new() { Name = $"Move {label} earlier", Exact = true }).ClickAsync();

    public Task MoveLaterAsync(string label) =>
        root.GetByRole(AriaRole.Button, new() { Name = $"Move {label} later", Exact = true }).ClickAsync();

    /// <summary>The chip's toggle button.</summary>
    public ILocator Toggle(string label) =>
        root.Locator(".ss-chip-toggle").Filter(new() { HasTextRegex = new Regex($"^\\s*{Regex.Escape(label)}\\s*$") });
}
