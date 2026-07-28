namespace EventDbLite.Abstractions;
public class ReactionEvent<T>
{
    public ReactionEvent(T payload, StreamEvent streamEvent)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        StreamEvent = streamEvent ?? throw new ArgumentNullException(nameof(streamEvent));
    }

    public T Payload { get; }
    public StreamEvent StreamEvent { get; }
}
