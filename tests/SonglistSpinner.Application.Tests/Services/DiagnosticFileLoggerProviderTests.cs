using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public sealed class DiagnosticFileLoggerProviderTests : IDisposable
{
    private const string EnabledEntry =
        "2026-10-03T12:00:00.0000000+00:00 [Information] SonglistSpinner.Services.DiagnosticFileLoggerProvider: " +
        "Diagnostic logging enabled.";

    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), "SonglistSpinnerTests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));

    private string LogPath => Path.Combine(_logDirectory, DiagnosticFileLoggerProvider.LogFileName);
    private string PreviousLogPath => Path.Combine(_logDirectory, DiagnosticFileLoggerProvider.PreviousLogFileName);

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
            Directory.Delete(_logDirectory, recursive: true);
    }

    [Fact]
    public void Given_DisabledLog_When_Logging_Then_WritesNoFile()
    {
        using var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);
        var logger = provider.CreateLogger("SonglistSpinner.Tests");

        logger.LogError("Spin failed for {QueueId}", 7);

        Assert.False(logger.IsEnabled(LogLevel.Error));
        Assert.False(File.Exists(LogPath));
    }

    [Fact]
    public void Given_EnabledLog_When_LoggingTemplate_Then_WritesTimestampLevelCategoryAndRenderedMessage()
    {
        var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);
        provider.SetEnabled(true);
        var logger = provider.CreateLogger("SonglistSpinner.Tests");

        logger.LogWarning("Refresh for {Streamer} failed; retrying in {RetryDelay}", "example", TimeSpan.FromSeconds(1));
        provider.Dispose();

        Assert.Equal(
            [
                EnabledEntry,
                "2026-10-03T12:00:00.0000000+00:00 [Warning] SonglistSpinner.Tests: " +
                "Refresh for example failed; retrying in 00:00:01"
            ],
            File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Given_EnabledLog_When_LoggingException_Then_WritesTheExceptionAfterTheMessage()
    {
        var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);
        provider.SetEnabled(true);
        var logger = provider.CreateLogger("SonglistSpinner.Tests");

        logger.LogError(new InvalidOperationException("The queue is unavailable."), "Spin failed");
        provider.Dispose();

        Assert.Equal(
            [
                EnabledEntry,
                "2026-10-03T12:00:00.0000000+00:00 [Error] SonglistSpinner.Tests: Spin failed",
                "System.InvalidOperationException: The queue is unavailable."
            ],
            File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Given_LogAtTheSizeLimit_When_Enabled_Then_MovesItToThePreviousLogAndStartsANewOne()
    {
        WriteLogOfLength(LogPath, DiagnosticFileLoggerProvider.MaximumLogBytes);
        WriteLogOfLength(PreviousLogPath, 10);
        var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);

        provider.SetEnabled(true);
        provider.Dispose();

        Assert.Equal(DiagnosticFileLoggerProvider.MaximumLogBytes, new FileInfo(PreviousLogPath).Length);
        Assert.Equal([EnabledEntry], File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Given_LogBelowTheSizeLimit_When_Enabled_Then_AppendsToIt()
    {
        File.WriteAllLines(EnsureDirectory(LogPath), ["earlier entry"]);
        var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);

        provider.SetEnabled(true);
        provider.Dispose();

        Assert.False(File.Exists(PreviousLogPath));
        Assert.Equal(["earlier entry", EnabledEntry], File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Given_EnabledLog_When_Disabled_Then_DropsLaterEntries()
    {
        var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);
        provider.SetEnabled(true);
        var logger = provider.CreateLogger("SonglistSpinner.Tests");

        provider.SetEnabled(false);
        logger.LogError("Spin failed");
        provider.Dispose();

        Assert.False(provider.IsEnabled);
        Assert.Equal([EnabledEntry], File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Given_EnabledLog_When_EnabledAgain_Then_KeepsTheOpenFile()
    {
        var provider = new DiagnosticFileLoggerProvider(_logDirectory, _timeProvider);
        provider.SetEnabled(true);

        provider.SetEnabled(true);
        provider.Dispose();

        Assert.Equal([EnabledEntry], File.ReadAllLines(LogPath));
    }

    private string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(_logDirectory);
        return path;
    }

    private void WriteLogOfLength(string path, long length)
    {
        using var file = File.Create(EnsureDirectory(path));
        file.SetLength(length);
    }
}
