using EventDbLite.Abstractions;
using EventDbLite.Reactions.Abstractions;
using EventDbLite.Streams;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Reactions;

public class EventDbLiteConstantReactionPositionStorage : IConstantReactionPositionStorage
{
    private readonly IEventSerializer _eventSerializer;
    private readonly IEventStoreLite _store;
    private readonly IStreamEventWriter _writer;

    public EventDbLiteConstantReactionPositionStorage(IEventSerializer eventSerializer, IEventStoreLite store, IStreamEventWriter writer)
    {
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    private static string GetStreamName(string reactionKey) => $"$reactions-{reactionKey}";
    public async Task<StreamPosition?> GetPositionAsync(string reactionKey)
    {
        string reactionEventIdentifier = _eventSerializer.GetIdentifier(typeof(ReactionHandled));

        await foreach (StreamEvent streamEvent in _store.ReadStreamEvents(GetStreamName(reactionKey), StreamDirection.Reverse, StreamPosition.End))
        {
            EventMetadata metadata = _eventSerializer.DeserializeMetadata(streamEvent.Data.Metadata);

            if (metadata.Identifier != reactionEventIdentifier)
            {
                continue;
            }

            ReactionHandled? handled = _eventSerializer.DeserializeEvent(streamEvent.Data.Payload, typeof(ReactionHandled)) as ReactionHandled;

            if (handled is null)
            {
                continue;
            }

            return StreamPosition.WithGlobalVersion(handled.GlobalOrdinal);
        }

        return StreamPosition.Beginning;
    }

    public Task SetPositionAsync(string reactionKey, long globalPosition)
    {
        ReactionHandled handledEvent = new()
        {
            GlobalOrdinal = globalPosition,
        };

        return _writer.AppendToStream(GetStreamName(reactionKey), handledEvent);
    }
}
