using EventDbLite.Abstractions;
using EventDbLite.Streams;
using KurrentDB.Client;

namespace EventDbLite.KurrentDb;

public class KurrentDbEventStoreLite : IEventStoreLite
{
    private readonly KurrentDBClient _client;

    public KurrentDbEventStoreLite(KurrentDBClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public static KurrentDB.Client.StreamState ToKurrentDbState(Abstractions.StreamState expectedState)
    {
        if (expectedState == Abstractions.StreamState.Any)
        {
            return KurrentDB.Client.StreamState.Any;
        }
        if (expectedState == Abstractions.StreamState.NoStream)
        {
            return KurrentDB.Client.StreamState.NoStream;
        }
        if (expectedState == Abstractions.StreamState.StreamExists)
        {
            return KurrentDB.Client.StreamState.StreamExists;
        }
        if (expectedState == Abstractions.StreamState.Start)
        {
            return KurrentDB.Client.StreamState.StreamExists;
        }
        if (expectedState == Abstractions.StreamPosition.End)
        {
            return KurrentDB.Client.StreamState.Any;
        }
        
        return KurrentDB.Client.StreamState.StreamRevision(expectedState);
    }
    public Task AppendToStreamAsync(string streamName, IEnumerable<Abstractions.EventData> data, Abstractions.StreamState expectedState)
    {
        KurrentDB.Client.StreamState kurrentDbState = ToKurrentDbState(expectedState);

        IEnumerable<KurrentDB.Client.EventData> kurrentDbEvents = data.Select(e => new KurrentDB.Client.EventData(Uuid.NewUuid(), e.Identifier, e.Payload, e.Metadata));

        return _client.AppendToStreamAsync(streamName, kurrentDbState, kurrentDbEvents);
    }

    public async IAsyncEnumerable<StreamEvent> ReadAllEvents(StreamDirection direction, Abstractions.Position fromPosition)
    {
        Direction kurrentDbDirection = direction == StreamDirection.Forward ? Direction.Forwards : Direction.Backwards;

        KurrentDB.Client.Position kurrentPosition = new(fromPosition.CommitPosition, fromPosition.PreparePosition);

        KurrentDBClient.ReadAllStreamResult streamResult = _client.ReadAllAsync(kurrentDbDirection, kurrentPosition);

        await foreach (ResolvedEvent resolvedEvent in streamResult)
        {
            yield return new StreamEvent(resolvedEvent.Event.EventId.ToGuid(),
                resolvedEvent.Event.EventStreamId,
                new Abstractions.StreamPosition(resolvedEvent.OriginalEvent.EventNumber),
                new Abstractions.Position(resolvedEvent.Event.Position.CommitPosition, resolvedEvent.Event.Position.PreparePosition),
                new Abstractions.EventData(resolvedEvent.Event.Data.ToArray(), resolvedEvent.Event.Metadata.ToArray(), resolvedEvent.Event.EventType)
            );
        }
    }

    public async IAsyncEnumerable<StreamEvent> ReadStreamEvents(string streamName, StreamDirection direction, Abstractions.StreamPosition fromPosition)
    {
        Direction kurrentDbDirection = direction == StreamDirection.Forward ? Direction.Forwards : Direction.Backwards;

        KurrentDB.Client.StreamPosition kurrentPosition = new(fromPosition);

        KurrentDBClient.ReadStreamResult streamResult = _client.ReadStreamAsync(kurrentDbDirection,streamName, kurrentPosition);

        await foreach (ResolvedEvent resolvedEvent in streamResult)
        {
            yield return new StreamEvent(resolvedEvent.Event.EventId.ToGuid(),
                resolvedEvent.Event.EventStreamId,
                new Abstractions.StreamPosition(resolvedEvent.OriginalEvent.EventNumber),
                new Abstractions.Position(resolvedEvent.Event.Position.CommitPosition, resolvedEvent.Event.Position.PreparePosition),
                new Abstractions.EventData(resolvedEvent.Event.Data.ToArray(), resolvedEvent.Event.Metadata.ToArray(), resolvedEvent.Event.EventType)
            );
        }
    }

    public IStreamSubscription SubscribeToAllStreams(Abstractions.Position from)
    {
        FromAll kurrentFrom = FromAll.After(new KurrentDB.Client.Position(from.CommitPosition, from.PreparePosition));

        return new KurrentDbStreamSubscription((token) => _client.SubscribeToAll(kurrentFrom, cancellationToken: token), null);
    }

    public IStreamSubscription SubscribeToStream(string streamName, Abstractions.StreamPosition from)
    {
        FromStream kurrentFrom = FromStream.After(from.Position);

        return new KurrentDbStreamSubscription((token) => _client.SubscribeToStream(streamName, kurrentFrom, cancellationToken: token), streamName);
    }
}
