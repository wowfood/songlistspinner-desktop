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
    public async Task SetSelectedAsync(string label, bool selected)
    {
        var toggle = Toggle(label);
        var pressed = selected ? "true" : "false";
        if (await toggle.GetAttributeAsync("aria-pressed") != pressed) await toggle.ClickAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", pressed);
    }

    public Task MoveEarlierAsync(string label) =>
        root.GetByRole(AriaRole.Button, new() { Name = $"Move {label} earlier", Exact = true }).ClickAsync();

    public Task MoveLaterAsync(string label) =>
        root.GetByRole(AriaRole.Button, new() { Name = $"Move {label} later", Exact = true }).ClickAsync();

    /// <summary>The chip's toggle button; <c>aria-pressed</c> is "true" while the field is shown.</summary>
    public ILocator Toggle(string label) =>
        root.Locator(".ss-chip-toggle").Filter(new() { HasTextRegex = new Regex($"^\\s*{Regex.Escape(label)}\\s*$") });
}
