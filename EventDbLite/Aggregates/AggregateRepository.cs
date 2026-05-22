using EventDbLite.Abstractions;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Streams;

namespace EventDbLite.Aggregates;

public class AggregateRepository(IEventStoreLite connection, IEventSerializer eventSerializer, IHandlerProvider aggregateHandlerProvider) : IAggregateRepository
{
    private readonly IEventStoreLite _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly IEventSerializer _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
    private readonly IHandlerProvider _aggregateHandlerProvider = aggregateHandlerProvider ?? throw new ArgumentNullException(nameof(aggregateHandlerProvider));

    private void Initialize(AggregateRoot aggregateRoot) => aggregateRoot.InitialiseDependencies(_aggregateHandlerProvider, _eventSerializer);

    public async Task<AggregateType?> Get<AggregateType>(string streamName) where AggregateType : AggregateRoot, new()
    {
        IAsyncEnumerable<StreamEvent> streamEvents = _connection.ReadStreamEvents(streamName, StreamDirection.Forward, StreamPosition.Start);

        AggregateType? aggregateRoot = null;

        await foreach (StreamEvent streamEvent in streamEvents)
        {
            if (aggregateRoot == null)
            {
                aggregateRoot = new();
                Initialize(aggregateRoot);
            }

            aggregateRoot.Raise(streamEvent);
        }

        return aggregateRoot;
    }
    public Task Save<AggregateType>(AggregateType aggregateRoot, string streamName, StreamState expectedState) where AggregateType : AggregateRoot
    {
        IEnumerable<EventData> raisedEvents = aggregateRoot.GetEvents();

        if (!raisedEvents.Any())
        {
            return Task.CompletedTask;
        }

        return _connection.AppendToStreamAsync(streamName, raisedEvents, expectedState);
    }

    public AggregateType CreateNew<AggregateType>(Func<AggregateType>? constructor) where AggregateType : AggregateRoot, new()
    {
        AggregateType aggregateRoot = constructor?.Invoke() ?? new();
        Initialize(aggregateRoot);
        return aggregateRoot;
    }
}
