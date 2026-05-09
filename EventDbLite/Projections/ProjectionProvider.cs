using EventDbLite.Abstractions;
using EventDbLite.Handlers;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;

namespace EventDbLite.Projections;

public class ProjectionProvider : IProjectionProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventStoreLite _connection;
    private readonly IEventSerializer _eventSerializer;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IHandlerProvider _handlerProvider;
    private readonly ILogger<ProjectionProvider> _logger;

    public ProjectionProvider(IServiceProvider serviceProvider, IEventStoreLite connection, IEventSerializer eventSerializer, ISnapshotRepository snapshotRepository, IHandlerProvider aggregateHandlerProvider, ILogger<ProjectionProvider> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _handlerProvider = aggregateHandlerProvider ?? throw new ArgumentNullException(nameof(aggregateHandlerProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string GetSnapshotKey<T>(string? streamName = null)
    {
        return $"{typeof(T).FullName}:{streamName??"$all"}";
    }

    public async Task<Projection<T>> CloneAsync<T>(string? streamName = null) where T : notnull
    {
        string snapshotKey = GetSnapshotKey<T>(streamName);

        T instance = ActivatorUtilities.GetServiceOrCreateInstance<T>(_serviceProvider);
        Position position = Position.Start;
        StreamPosition streamPosition = StreamPosition.Start;

        await foreach(Snapshot snapshot in _snapshotRepository.GetSnapshots(snapshotKey))
        {
            Handler? handler = _handlerProvider.GetRestoreHandler(instance.GetType(), snapshot.Identifier);
            
            if(handler == null)
            {
                continue;
            }

            object? payload = _eventSerializer.DeserializeEvent(snapshot.Data, handler.TargetType)
                ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{snapshot.Identifier}'");

            if(payload == null)
            {
                continue;
            }

            handler.Action.Invoke(instance, payload);
            position = snapshot.Position;
            streamPosition = snapshot.StreamPosition;
        }

        _logger.LogInformation("Cloned projection {ProjectionType} on stream '{StreamName}' at position {Position} stream position {StreamPosition}", typeof(T).Name, streamName ?? "$all", position, streamPosition);

        return new Projection<T>(instance, streamName, position, streamPosition);
    }
    public async Task<PulledProjection<T>> PullAsync<T>(Projection<T> projection, Position until) where T : notnull
    {
        IAsyncEnumerable<StreamEvent> streamEvents = projection.StreamName is null 
            ? _connection.ReadAllEvents(StreamDirection.Forward, projection.StartPosition) 
            : _connection.ReadStreamEvents(projection.StreamName, StreamDirection.Forward, projection.StreamStartPosition);

        long appliedEvents = 0;
        long passedEvents = 0;
        Position lastPosition = projection.StartPosition;
        StreamPosition lastStreamPosition = projection.StreamStartPosition;

        await foreach(StreamEvent streamEvent in streamEvents)
        {
            if(streamEvent.GlobalOrdinal.IsAfter(until))
            {
                break;
            }

            passedEvents++;
            EventMetadata? metadata = _eventSerializer.DeserializeMetadata(streamEvent.Data.Metadata);

            if (metadata is null)
            {
                continue;
            }

            Handler? handler = _handlerProvider.GetHandlerMethod(projection.Object.GetType(), metadata.Identifier);
            if (handler is null)
            {
                continue;
            }

            object? payload = _eventSerializer.DeserializeEvent(streamEvent.Data.Payload, handler.TargetType)
                ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{metadata.Identifier}'");

            handler.Action(projection.Object, payload);
            appliedEvents++;
            lastPosition = streamEvent.GlobalOrdinal;
            lastStreamPosition = streamEvent.StreamOrdinal;
        }

        _logger.LogInformation("Pulled projection {ProjectionType} on stream '{StreamName}': {AppliedEvents} events applied out of {PassedEvents} passed from position {StartPosition} to {FinalPosition} stream position {StartStreamPosition} to {FinalStreamPosition}", typeof(T).Name, projection.StreamName ?? "$all", appliedEvents, passedEvents, projection.StartPosition, lastPosition, projection.StreamStartPosition, lastStreamPosition);

        return new PulledProjection<T>(projection.Object, projection.StreamName, lastPosition, lastStreamPosition, appliedEvents, passedEvents, lastStreamPosition, lastPosition);
    }
    public async Task PushAsync<T>(PulledProjection<T> projection) where T : notnull
    {
        if(projection.AppliedEvents == 0)
        {
            _logger.LogDebug("Push skipped for projection {ProjectionType} on stream '{StreamName}': no events were applied", typeof(T).Name, projection.StreamName ?? "$all");
            return;
        }

        SnapshotHandler? handler = _handlerProvider.GetSnapshotHandler(projection.Object.GetType());

        if(handler is null)
        {
            return;
        }

        object? payload = handler.Action(projection.Object);

        if (payload is null)
        {
            return;
        }

        string snapshotKey = GetSnapshotKey<T>(projection.StreamName);
        string identifier = _eventSerializer.GetIdentifier(payload.GetType());

        byte[] data = _eventSerializer.SerializeEvent(payload);

        Snapshot snapshot = new (data, identifier, projection.FinalPosition, projection.FinalStreamPosition);

        await _snapshotRepository.StoreSnapshot(snapshotKey, snapshot);
        _logger.LogInformation("Pushed projection {ProjectionType} on stream '{StreamName}': snapshot saved after {AppliedEvents} applied events at position {Position} stream position {StreamPosition}", typeof(T).Name, projection.StreamName ?? "$all", projection.AppliedEvents, projection.FinalPosition, projection.FinalStreamPosition);
    }

    public T CreateInstance<T>() => ActivatorUtilities.GetServiceOrCreateInstance<T>(_serviceProvider);
}
