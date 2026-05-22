using EventDbLite.Abstractions;
using Google.Protobuf;
using KurrentDB.Client;
using System.Runtime.CompilerServices;
using System.Threading;

namespace EventDbLite.KurrentDb;
public sealed class KurrentDbStreamSubscription : IStreamSubscription
{
    private readonly Func<CancellationToken, KurrentDBClient.StreamSubscriptionResult> _subscriptionDelegate;
    public string? StreamName { get; }

    public KurrentDbStreamSubscription(Func<CancellationToken, KurrentDBClient.StreamSubscriptionResult> subscriptionDelegate, string? streamName)
    {
        _subscriptionDelegate = subscriptionDelegate ?? throw new ArgumentNullException(nameof(subscriptionDelegate));
        StreamName = streamName;
    }

    public async IAsyncEnumerable<SubscriptionEvent> CatchUp(CancellationToken token)
    {
        yield break;
    }

    public async IAsyncEnumerable<SubscriptionEvent> StreamEvents([EnumeratorCancellation]CancellationToken token)
    {
        KurrentDBClient.StreamSubscriptionResult subscriptionResult = _subscriptionDelegate(token);

        await foreach (StreamMessage message in subscriptionResult.Messages.WithCancellation(token))
        {
            switch (message)
            {
                case StreamMessage.Event eventMessage:
                    {
                        yield return new SubscriptionEvent(true,
                            new StreamEvent(eventMessage.ResolvedEvent.Event.EventId.ToGuid(),
                                eventMessage.ResolvedEvent.Event.EventStreamId,
                                new Abstractions.StreamPosition(eventMessage.ResolvedEvent.Event.EventNumber),
                                new Abstractions.Position(eventMessage.ResolvedEvent.Event.Position.CommitPosition, eventMessage.ResolvedEvent.Event.Position.PreparePosition),
                                new Abstractions.EventData(eventMessage.ResolvedEvent.Event.Data.ToArray(),
                                eventMessage.ResolvedEvent.Event.Metadata.ToArray(),
                                eventMessage.ResolvedEvent.Event.EventType)
                            ));
                        break;
                    }
                default:
                    break;
            }
        }
    }

    public void Dispose()
    {
    }
}
