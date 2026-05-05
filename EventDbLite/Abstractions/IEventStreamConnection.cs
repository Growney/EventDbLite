using EventDbLite.Streams;

namespace EventDbLite.Abstractions;

public interface IEventStreamConnection
{
    IAsyncEnumerable<StreamEvent> ReadStreamEvents(string streamName, StreamDirection direction, StreamPosition fromPosition);
    IAsyncEnumerable<StreamEvent> ReadAllStreamEvents(StreamDirection direction, Position fromPosition);

    Task<IEnumerable<StreamEvent>> AppendToStreamAsync(string streamName, IEnumerable<EventData> data, StreamState expectedState);
    Task<StreamEvent> AppendToStreamAsync(string streamName, EventData data, StreamState expectedState);
}
