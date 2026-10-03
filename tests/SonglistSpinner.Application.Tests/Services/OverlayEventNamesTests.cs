using System.Reflection;
using System.Text.RegularExpressions;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class OverlayEventNamesTests
{
    [Fact]
    public void Given_OverlayContractsScript_When_ComparedWithOverlayEventNames_Then_BothListTheSameEvents()
    {
        var contracts = DesktopWebAssets.Read("overlay/SongSpinner.contracts.js");

        var scriptEvents = Regex.Match(contracts, @"overlayEvents: Object\.freeze\(\{(?<body>[^}]*)\}\)");

        Assert.True(scriptEvents.Success, "SongSpinner.contracts.js no longer declares overlayEvents.");
        Assert.Equal(
            typeof(OverlayEventNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue()!)
                .Order(StringComparer.Ordinal),
            Regex.Matches(scriptEvents.Groups["body"].Value, @"\w+: '(?<name>[^']+)'")
                .Select(match => match.Groups["name"].Value)
                .Order(StringComparer.Ordinal));
    }
}
