using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace SonglistSpinner.EndToEndTests.Infrastructure;

/// <remarks>
/// These run in every test run, without the end-to-end opt-in: they start no app. The stamp reader is what
/// keeps the end-to-end tests from starting an app on the developer's own settings and saved credential, so it
/// is tested against assemblies written for the purpose.
/// </remarks>
public sealed class TestProfileStampTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "SonglistSpinnerTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Given_AnAssemblyStampedWithTheTestProfile_When_ReadingTheStamp_Then_ItIsOn()
    {
        var assembly = WriteAssembly(("SonglistSpinner.TestProfile", "enabled"));

        Assert.True(TestProfileStamp.IsOn(assembly));
    }

    [Fact]
    public void Given_TheStampAmongOtherMetadata_When_ReadingTheStamp_Then_ItIsOn()
    {
        var assembly = WriteAssembly(("RepositoryUrl", "https://example.test"), ("SonglistSpinner.TestProfile", "enabled"));

        Assert.True(TestProfileStamp.IsOn(assembly));
    }

    [Fact]
    public void Given_AnAssemblyWithNoMetadata_When_ReadingTheStamp_Then_ItIsOff()
    {
        var assembly = WriteAssembly();

        Assert.False(TestProfileStamp.IsOn(assembly));
    }

    [Theory]
    [InlineData("SonglistSpinner.TestProfile", "disabled")]
    [InlineData("SonglistSpinner.TestProfile", "")]
    [InlineData("SonglistSpinner.AnotherStamp", "enabled")]
    // The key and value the wrong way round.
    [InlineData("enabled", "SonglistSpinner.TestProfile")]
    public void Given_AnAssemblyWithOtherMetadata_When_ReadingTheStamp_Then_ItIsOff(string key, string value)
    {
        var assembly = WriteAssembly((key, value));

        Assert.False(TestProfileStamp.IsOn(assembly));
    }

    /// <summary>Writes an assembly that carries <paramref name="metadata"/> as AssemblyMetadata attributes.</summary>
    private string WriteAssembly(params (string Key, string Value)[] metadata)
    {
        Directory.CreateDirectory(_directory);
        var name = "Stamped" + Guid.NewGuid().ToString("N");
        var builder = new PersistedAssemblyBuilder(new AssemblyName(name), typeof(object).Assembly);
        var constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
        foreach (var (key, value) in metadata)
            builder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [key, value]));
        builder.DefineDynamicModule(name);

        var path = Path.Combine(_directory, name + ".dll");
        builder.Save(path);
        return path;
    }
}
