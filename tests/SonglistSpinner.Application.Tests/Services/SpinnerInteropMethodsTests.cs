using System.Reflection;
using System.Text.RegularExpressions;
using SonglistSpinner.Services;
using SonglistSpinner.Testing;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class SpinnerInteropMethodsTests
{
    private const string SpinnerInteropPrefix = "SpinnerInterop.";

    [Fact]
    public void Given_SharedInteropScript_When_ComparedWithSpinnerInteropMethods_Then_BothListTheSameMethods()
    {
        var interop = DesktopWebAssets.Read("spinner/SongSpinner.interop.js");

        // The exported methods are the members of the object the SpinnerInterop factory returns.
        var returnedObject = interop[interop.IndexOf("\n    return {", StringComparison.Ordinal)..];
        var exports = Regex.Matches(returnedObject, @"^ {8}(?<name>\w+)\([^)]*\) \{\r?$", RegexOptions.Multiline)
            .Select(match => SpinnerInteropPrefix + match.Groups["name"].Value)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            typeof(SpinnerInteropMethods)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue()!)
                .Order(StringComparer.Ordinal),
            exports);
    }

    [Theory]
    [InlineData("spinner/SongSpinner.interop.js")]
    [InlineData("settings/DisplayFieldOrder.interop.js")]
    [InlineData("overlay/Overlay.html")]
    public void Given_WebAssetScript_When_CallingBackIntoDotNet_Then_TheMethodNameComesFromCSharp(string pathUnderWwwroot)
    {
        var script = DesktopWebAssets.Read(pathUnderWwwroot);

        // C# passes [JSInvokable] names with nameof, so a rename can't silently break the callback.
        var hardCodedCallbacks = Regex.Matches(script, @"invokeMethodAsync\(\s*['""`]")
            .Select(match => match.Value);

        Assert.Empty(hardCodedCallbacks);
    }
}
