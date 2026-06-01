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

    public async IAsyncEnumerable<SubscriptionMessage> Messages([EnumeratorCancellation]CancellationToken token)
    {
        KurrentDBClient.StreamSubscriptionResult subscriptionResult = _subscriptionDelegate(token);

        await foreach (StreamMessage message in subscriptionResult.Messages.WithCancellation(token))
        {
            switch (message)
            {
                case StreamMessage.Event eventMessage:
                    {
                        yield return new SubscriptionMessage.Event(
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
                case StreamMessage.Ok: yield return new SubscriptionMessage.Ok(); 
                    break;
                case StreamMessage.NotFound: yield return new SubscriptionMessage.NotFound();
                    break;
                case StreamMessage.FirstStreamPosition firstStream: yield return new SubscriptionMessage.FirstStreamPosition(new Abstractions.StreamPosition(firstStream.StreamPosition.ToUInt64()));
                    break;
                case StreamMessage.LastStreamPosition lastPosition: yield return new SubscriptionMessage.LastStreamPosition(new Abstractions.StreamPosition(lastPosition.StreamPosition.ToUInt64()));
                    break;
                case StreamMessage.LastAllStreamPosition lastAllStream: yield return new SubscriptionMessage.LastAllStreamPosition(new Abstractions.Position(lastAllStream.Position.CommitPosition, lastAllStream.Position.PreparePosition));
                    break;
                case StreamMessage.SubscriptionConfirmation confirmation: yield return new SubscriptionMessage.SubscriptionConfirmation(confirmation.SubscriptionId);
                    break;
                case StreamMessage.AllStreamCheckpointReached checkpoint: yield return new SubscriptionMessage.AllStreamCheckpointReached(new Abstractions.Position(checkpoint.Position.CommitPosition, checkpoint.Position.PreparePosition));
                    break;
                case StreamMessage.StreamCheckpointReached streamCheckpoint: yield return new SubscriptionMessage.StreamCheckpointReached(new Abstractions.StreamPosition(streamCheckpoint.StreamPosition));
                    break;
                case StreamMessage.CaughtUp: yield return new SubscriptionMessage.CaughtUp();
                    break;
                case StreamMessage.FellBehind: yield return new SubscriptionMessage.FellBehind();
                    break;
                default:
                    yield return new SubscriptionMessage.Unknown();
                    break;
            }
        }
    }

    public void Dispose()
    {
    }
}
