using EventDbLite.Streams;

namespace EventDbLite.Abstractions;

public interface IStreamSubscription : IDisposable
{
    IAsyncEnumerable<SubscriptionMessage> Messages(CancellationToken token);
}
