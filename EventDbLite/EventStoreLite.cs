using EventDbLite.Abstractions;
using EventDbLite.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EventDbLite;

public class EventStoreLite(IServiceProvider serviceProvider) : IEventStoreLite
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, StreamSubscription>> _streamSubscriptions = new();
    private readonly ConcurrentDictionary<Guid, StreamSubscription> _allStreamSubscriptions = new();

    public async Task AppendToStreamAsync(string streamName, IEnumerable<EventData> data, StreamState expectedState)
    {
        if (string.IsNullOrEmpty(streamName))
        {
            throw new ArgumentException("Stream name cannot be null or empty.", nameof(streamName));
        }

        using IServiceScope scope = _serviceProvider.CreateScope();

        IEventStreamConnection connection = scope.ServiceProvider.GetRequiredService<IEventStreamConnection>();

        IEnumerable<StreamEvent> storedEvents = await connection.AppendToStreamAsync(streamName, data, expectedState).ConfigureAwait(false);

        DispatchEvents(streamName, storedEvents);
    }

    private void DispatchEvents(string streamName, IEnumerable<StreamEvent> events)
    {
        foreach (StreamEvent streamEvent in events)
        {
            foreach (StreamSubscription subscription in _allStreamSubscriptions.Values)
            {
                subscription.AddLiveEvent(streamEvent);
            }
        }

        if (_streamSubscriptions.TryGetValue(streamName, out var streamSubscriptions))
        {
            foreach (StreamEvent streamEvent in events)
            {
                foreach (StreamSubscription subscription in streamSubscriptions.Values)
                {
                    subscription.AddLiveEvent(streamEvent);
                }
            }
        }
    }
    public async IAsyncEnumerable<StreamEvent> ReadStreamEvents(string streamName, StreamDirection direction, StreamPosition fromPosition)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        IEventStreamConnection connection = scope.ServiceProvider.GetRequiredService<IEventStreamConnection>();

        await foreach (StreamEvent streamEvent in connection.ReadStreamEvents(streamName, direction, fromPosition))
        {
            yield return streamEvent;
        }
    }
    public async IAsyncEnumerable<StreamEvent> ReadAllEvents(StreamDirection direction, Position fromPosition)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        IEventStreamConnection connection = scope.ServiceProvider.GetRequiredService<IEventStreamConnection>();

        await foreach (StreamEvent streamEvent in connection.ReadAllStreamEvents(direction, fromPosition))
        {
            yield return streamEvent;
        }
    }

    public IStreamSubscription SubscribeToAllStreams(Position position)
    {
        return CreateSubscription(position);
    }
    public IStreamSubscription SubscribeToStream(string streamName, StreamPosition position)
    {
        if (string.IsNullOrEmpty(streamName))
        {
            throw new ArgumentException("Stream name cannot be null or empty.", nameof(streamName));
        }
        return CreateSubscription(streamName, position);
    }

    private IStreamSubscription CreateSubscription(string streamName, StreamPosition initialPosition)
    {
        Guid subscriptionId = Guid.NewGuid();
        void onDispose(StreamSubscription subscription)
        {
            if (_streamSubscriptions.TryGetValue(streamName, out var streamSubscriptions))
            {
                streamSubscriptions.TryRemove(subscriptionId, out _);
            }
        }
        ILogger<StreamSubscription>? logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<StreamSubscription>();
        StreamSubscription subscription = new(logger, this, streamName, initialPosition, onDispose);
        _streamSubscriptions.GetOrAdd(streamName, _ => new ConcurrentDictionary<Guid, StreamSubscription>()).TryAdd(subscriptionId, subscription);

        return subscription;
    }

    private IStreamSubscription CreateSubscription(Position initialPosition)
    {
        Guid subscriptionId = Guid.NewGuid();

        void onDispose(StreamSubscription subscription)
        {
            _allStreamSubscriptions.TryRemove(subscriptionId, out _);
        }

        ILogger<StreamSubscription>? logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<StreamSubscription>();

        StreamSubscription subscription = new(logger, this, initialPosition, onDispose);
        _allStreamSubscriptions.TryAdd(subscriptionId, subscription);
        return subscription;
    }
}
