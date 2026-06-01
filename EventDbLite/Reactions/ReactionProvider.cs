using EventDbLite.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EventDbLite.Reactions;

public class ReactionProvider<TEvent> : IAsyncEnumerable<ReactionEvent<TEvent>>
{
    private readonly IEventStoreLite _store;
    private readonly IEventSerializer _eventSerializer;
    private readonly StreamPosition? _initialStreamPosition;
    private readonly Position? _initialPosition;
    private readonly string? _streamName;
    private readonly ILogger<ReactionProvider<TEvent>> _logger;

    public ReactionProvider(IEventSerializer eventSerializer, ILogger<ReactionProvider<TEvent>> logger, IEventStoreLite store, string? streamName, StreamPosition initialPosition)
    {
        _store = store;
        _eventSerializer = eventSerializer;
        _initialStreamPosition = initialPosition;
        _logger = logger;
        _streamName = streamName;
    }
    public ReactionProvider(IEventSerializer eventSerializer, ILogger<ReactionProvider<TEvent>> logger, IEventStoreLite store, Position initialPosition)
    {
        _store = store;
        _eventSerializer = eventSerializer;
        _initialPosition = initialPosition;
        _logger = logger;
    }

    public async IAsyncEnumerator<ReactionEvent<TEvent>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        string identifier = _eventSerializer.GetIdentifier(typeof(TEvent));

        IStreamSubscription subscription = _streamName is not null
           ? _store.SubscribeToStream(_streamName, _initialStreamPosition ?? StreamPosition.Start)
           : _store.SubscribeToAllStreams(_initialPosition ?? Position.Start);

        try
        {
            await foreach (SubscriptionEvent streamEvent in subscription.Messages(cancellationToken))
            {
                EventMetadata? metadata = _eventSerializer.DeserializeMetadata(streamEvent.Event.Data.Metadata);

                if(metadata is null)
                {
                    continue;
                }

                if (!metadata.Identifier.Equals(identifier))
                {
                    continue;
                }
                object? eventObject = _eventSerializer.DeserializeEvent(streamEvent.Event.Data.Payload, typeof(TEvent));

                if (eventObject is TEvent tEvent)
                {
                    yield return new ReactionEvent<TEvent>(tEvent, streamEvent);
                }
            }
        }
        finally
        {
            subscription.Dispose();
        }
    }
}
