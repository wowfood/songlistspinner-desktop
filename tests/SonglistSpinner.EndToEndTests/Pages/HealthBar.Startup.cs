using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace SonglistSpinner.EndToEndTests.Pages;

internal sealed partial class HealthBar
{
    /// <summary>For details that carry the local time, such as "Queue and history last synchronized at ...".</summary>
    public Task ExpectApiDetailAsync(Regex detail) => Expect(Item("API: ")).ToHaveAttributeAsync("title", detail);
}
