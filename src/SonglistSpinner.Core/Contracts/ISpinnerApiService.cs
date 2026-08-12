using SonglistSpinner.Core.Models;

namespace SonglistSpinner.Core.Contracts;

public interface ISpinnerApiService
{
    Task<SpinnerQueueItem[]> FetchQueueAsync(string streamer);
    Task<PlayHistoryItem[]> FetchPlayHistoryAsync(string streamer, string period = "week");
    Task<SpinnerConfig?> FetchConfigAsync(string configUrl);
}