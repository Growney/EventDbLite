namespace EventDbLite.Abstractions;

public class StreamEvent
{
    public Guid Id { get; }
    public string StreamName { get; }
    public StreamPosition StreamOrdinal { get; }
    public Position GlobalOrdinal { get; }
    public EventData Data { get; }

    public StreamEvent(Guid id, string streamName, StreamPosition streamOrdinal, Position globalOrdinal, EventData data)
    {
        Id = id;
        StreamName = streamName ?? throw new ArgumentNullException(nameof(streamName));
        StreamOrdinal = streamOrdinal;
        GlobalOrdinal = globalOrdinal;
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }
}
