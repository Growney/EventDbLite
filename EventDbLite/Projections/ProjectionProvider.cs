using EventDbLite.Abstractions;
using EventDbLite.Handlers;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
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
        return $"{typeof(T).FullName}:{streamName ?? "all"}";
    }

    public T CreateInstance<T>() => ActivatorUtilities.GetServiceOrCreateInstance<T>(_serviceProvider);

    public async Task<StreamProjection<T>> CloneAsync<T>(string streamName, StreamPosition until) where T : notnull
    {
        string snapshotKey = GetSnapshotKey<T>();

        T instance = ActivatorUtilities.GetServiceOrCreateInstance<T>(_serviceProvider);
        StreamPosition streamPosition = StreamPosition.Start;

        await foreach (IReadSnapshot snapshot in _snapshotRepository.GetSnapshots(snapshotKey))
        {
            Handler? handler = _handlerProvider.GetRestoreHandler(instance.GetType(), snapshot.Identifier);

            if (handler == null)
            {
                continue;
            }

            if (snapshot.StreamPosition.IsAfter(until))
            {
                continue;
            }

            object? payload = _snapshotRepository.DeserializeSnapshot(snapshot, handler.TargetType)
                ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{snapshot.Identifier}'");

            if (payload == null)
            {
                continue;
            }

            handler.Action.Invoke(instance, payload);
            streamPosition = snapshot.StreamPosition;

            break;
        }

        _logger.LogInformation("Cloned projection {ProjectionType} on stream '{StreamName}' at stream position {StreamPosition}", typeof(T).Name, streamName, streamPosition);

        return new StreamProjection<T>(instance, streamName, streamPosition, until);
    }

    public async Task<AllStreamProjection<T>> CloneAsync<T>(Position until) where T : notnull
    {
        string snapshotKey = GetSnapshotKey<T>();

        T instance = ActivatorUtilities.GetServiceOrCreateInstance<T>(_serviceProvider);
        Position position = Position.Start;

        await foreach (IReadSnapshot snapshot in _snapshotRepository.GetSnapshots(snapshotKey))
        {
            Handler? handler = _handlerProvider.GetRestoreHandler(instance.GetType(), snapshot.Identifier);

            if (handler == null)
            {
                continue;
            }

            if (snapshot.Position.IsAfter(until))
            {
                continue;
            }

            object? payload = _snapshotRepository.DeserializeSnapshot(snapshot, handler.TargetType)
                ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{snapshot.Identifier}'");

            if (payload == null)
            {
                continue;
            }

            handler.Action.Invoke(instance, payload);
            position = snapshot.Position;

            break;
        }

        _logger.LogInformation("Cloned projection {ProjectionType} on stream '{StreamName}' at position {Position}", typeof(T).Name, "$all", position);

        return new AllStreamProjection<T>(instance, position, until);
    }

    public async Task<PulledStreamProjection<T>> PullAsync<T>(StreamProjection<T> projection) where T : notnull
    {
        IAsyncEnumerable<StreamEvent> streamEvents = _connection.ReadStreamEvents(projection.StreamName, StreamDirection.Forward, projection.StartPosition);

        long appliedEvents = 0;
        long passedEvents = 0;
        StreamPosition lastStreamPosition = projection.StartPosition;

        await foreach (StreamEvent streamEvent in streamEvents)
        {
            if (streamEvent.StreamOrdinal.IsAfter(projection.TargetPosition))
            {
                break;
            }

            passedEvents++;

            Handler? handler = _handlerProvider.GetHandlerMethod(projection.Object.GetType(), streamEvent.Data.Identifier);
            if (handler is null)
            {
                continue;
            }

            object? payload = _eventSerializer.DeserializeEvent(streamEvent.Data.Payload, handler.TargetType)
                ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{streamEvent.Data.Identifier}'");

            if(projection.Object is ContextProjection context)
            {
                EventMetadata? metadata = _eventSerializer.DeserializeMetadata(streamEvent.Data.Metadata);
                context.Metadata = metadata;
            }

            handler.Action(projection.Object, payload);
            appliedEvents++;
            lastStreamPosition = streamEvent.StreamOrdinal;
        }

        _logger.LogInformation("Pulled projection {ProjectionType} on stream '{StreamName}': {AppliedEvents} events applied out of {PassedEvents} passed from position {StartPosition} to {FinalPosition}", typeof(T).Name, projection.StreamName, appliedEvents, passedEvents, projection.StartPosition, lastStreamPosition);

        return new PulledStreamProjection<T>(projection.Object, projection.StreamName,projection.StartPosition, projection.TargetPosition, appliedEvents, passedEvents, lastStreamPosition);
    }

    public async Task<PulledAllStreamProjection<T>> PullAsync<T>(AllStreamProjection<T> projection) where T : notnull
    {
        IAsyncEnumerable<StreamEvent> streamEvents = _connection.ReadAllEvents(StreamDirection.Forward, projection.StartPosition);

        long appliedEvents = 0;
        long passedEvents = 0;
        Position lastPosition = projection.StartPosition;

        await foreach (StreamEvent streamEvent in streamEvents)
        {
            if (streamEvent.GlobalOrdinal.IsAfter(projection.TargetPosition))
            {
                break;
            }

            passedEvents++;

            Handler? handler = _handlerProvider.GetHandlerMethod(projection.Object.GetType(), streamEvent.Data.Identifier);
            if (handler is null)
            {
                continue;
            }

            object? payload = _eventSerializer.DeserializeEvent(streamEvent.Data.Payload, handler.TargetType)
                ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{streamEvent.Data.Identifier}'");

            if (projection.Object is ContextProjection context)
            {
                EventMetadata? metadata = _eventSerializer.DeserializeMetadata(streamEvent.Data.Metadata);
                context.Metadata = metadata;
            }

            handler.Action(projection.Object, payload);
            appliedEvents++;
            lastPosition = streamEvent.GlobalOrdinal;
        }

        _logger.LogInformation("Pulled projection {ProjectionType} on stream '{StreamName}': {AppliedEvents} events applied out of {PassedEvents} passed from position {StartPosition} to {FinalPosition}", typeof(T).Name,"$all", appliedEvents, passedEvents, projection.StartPosition, lastPosition);

        return new PulledAllStreamProjection<T>(projection.Object, lastPosition, projection.TargetPosition, appliedEvents, passedEvents, lastPosition);
    }

    public async Task PushAsync<T>(PulledStreamProjection<T> projection) where T : notnull
    {
        if (projection.AppliedEvents == 0)
        {
            _logger.LogDebug("Push skipped for projection {ProjectionType} on stream '{StreamName}': no events were applied", typeof(T).Name, projection.StreamName);
            return;
        }

        SnapshotHandler? handler = _handlerProvider.GetSnapshotHandler(projection.Object.GetType());

        if (handler is null)
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

        await _snapshotRepository.StoreSnapshot(snapshotKey, payload, identifier, Position.Start, projection.FinalPosition);
        _logger.LogInformation("Pushed projection {ProjectionType} on stream '{StreamName}': snapshot saved after {AppliedEvents} applied events at position {Position}", typeof(T).Name, projection.StreamName, projection.AppliedEvents, projection.FinalPosition);
    }

    public async Task PushAsync<T>(PulledAllStreamProjection<T> projection) where T : notnull
    {
        if (projection.AppliedEvents == 0)
        {
            _logger.LogDebug("Push skipped for projection {ProjectionType} on stream '{StreamName}': no events were applied", typeof(T).Name, "$all");
            return;
        }

        SnapshotHandler? handler = _handlerProvider.GetSnapshotHandler(projection.Object.GetType());

        if (handler is null)
        {
            return;
        }

        object? payload = handler.Action(projection.Object);

        if (payload is null)
        {
            return;
        }

        string snapshotKey = GetSnapshotKey<T>();
        string identifier = _eventSerializer.GetIdentifier(payload.GetType());

        await _snapshotRepository.StoreSnapshot(snapshotKey, payload, identifier, projection.FinalPosition, StreamPosition.Start);
        _logger.LogInformation("Pushed projection {ProjectionType} on stream '{StreamName}': snapshot saved after {AppliedEvents} applied events at position {Position}", typeof(T).Name, "$all", projection.AppliedEvents, projection.FinalPosition);
    }
}
