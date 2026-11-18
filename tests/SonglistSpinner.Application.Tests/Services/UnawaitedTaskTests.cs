using SonglistSpinner.Services;
using Xunit;

namespace SonglistSpinner.Application.Tests.Services;

public class UnawaitedTaskTests
{
    [Fact]
    public void Given_FaultedTask_When_ObserveFaults_Then_ReportsTheOriginalException()
    {
        var failure = new InvalidOperationException("Background work failed.");
        Exception? reported = null;

        Task.FromException(failure).ObserveFaults(ex => reported = ex);

        Assert.Same(failure, reported);
    }

    [Fact]
    public void Given_SuccessfulTask_When_ObserveFaults_Then_ReportsNothing()
    {
        var reported = false;

        Task.CompletedTask.ObserveFaults(_ => reported = true);

        Assert.False(reported);
    }

    [Fact]
    public void Given_CancelledTask_When_ObserveFaults_Then_ReportsNothing()
    {
        var reported = false;

        Task.FromCanceled(new CancellationToken(canceled: true)).ObserveFaults(_ => reported = true);

        Assert.False(reported);
    }

    [Fact]
    public async Task Given_TaskThatFaultsLater_When_ObserveFaults_Then_ReportsTheExceptionWhenItFaults()
    {
        var work = new TaskCompletionSource();
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Background work failed.");
        work.Task.ObserveFaults(ex => reported.TrySetResult(ex));

        work.SetException(failure);

        Assert.Same(failure, await reported.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}
