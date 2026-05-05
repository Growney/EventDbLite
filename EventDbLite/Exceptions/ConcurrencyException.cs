using EventDbLite.Abstractions;

namespace EventDbLite.Exceptions;

public class ConcurrencyException(StreamState expectedVersion, StreamPosition actualVersion) : Exception($"Concurrency conflict: expected version {expectedVersion}, but actual version is {actualVersion}.")
{
    public StreamState ExpectedVersion { get; } = expectedVersion;
    public StreamPosition ActualVersion { get; } = actualVersion;

    public static async Task Retry(Func<Task> action, int maxRetries = int.MaxValue, int delayMilliseconds = 2, float delayScale = 1)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch (ConcurrencyException)
            {
                attempt++;
                if (attempt > maxRetries)
                {
                    throw;
                }
                delayMilliseconds = (int)(attempt * delayScale * delayMilliseconds);
                await Task.Delay(delayMilliseconds);
            }
        }
    }
}
