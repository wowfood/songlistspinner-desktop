namespace SonglistSpinner.Services;

public static class UnawaitedTask
{
    /// <summary>
    /// Reports the fault of a task that is deliberately not awaited, so the failure is recorded when it
    /// happens instead of surfacing later, if at all, as an unobserved task exception.
    /// </summary>
    /// <remarks>
    /// <paramref name="onFault"/> runs only for a faulted task, never for success or cancellation. It
    /// receives the task's single exception, or the whole <see cref="AggregateException"/> when there are
    /// several. It runs on the thread that completes the task, or inline when the task has already
    /// faulted, so it must be quick and must not throw.
    /// </remarks>
    public static void ObserveFaults(this Task task, Action<Exception> onFault)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onFault);

        task.ContinueWith(
            faulted =>
            {
                if (faulted.Exception is not { } exception) return;
                onFault(exception.InnerExceptions.Count == 1 ? exception.InnerExceptions[0] : exception);
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
