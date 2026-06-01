using EventDbLite.Abstractions;
using EventDbLite.Handlers;
using EventDbLite.Reactions.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Projections;

public class TrackedProjectionService : BackgroundService
{
    private readonly IEnumerable<TrackedProjectionRequirement> _trackedProjections;
    private readonly IProjectionProvider _projectionProvider;
    private readonly IServiceProvider _serviceProvider;

    public TrackedProjectionService(IEnumerable<TrackedProjectionRequirement> trackedProjections, IProjectionProvider projectionProvider, IServiceProvider serviceProvider)
    {
        _trackedProjections = trackedProjections ?? throw new ArgumentNullException(nameof(trackedProjections));
        _projectionProvider = projectionProvider ?? throw new ArgumentException(nameof(projectionProvider));
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }

    private async Task TrackProjection(Type projectionType)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        IEventStoreLite store = scope.ServiceProvider.GetRequiredService<IEventStoreLite>();

        IConstantReactionPositionStorage positionStorage = scope.ServiceProvider.GetRequiredKeyedService<IConstantReactionPositionStorage>(storageKey);
        //The position that is stored is the position of the last event that was successfully reacted to, so we need to start from the next position
        Position position = await positionStorage.GetPositionAsync(reactionKey) ?? Position.Start;

        IStreamSubscription subscription = store.SubscribeToAllStreams(position);

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
        }

        _logger.LogInformation("Pulled projection {ProjectionType} on stream '{StreamName}': {AppliedEvents} events applied out of {PassedEvents} passed from position {StartPosition} to {FinalPosition}", typeof(T).Name, "$all", appliedEvents, passedEvents, projection.StartPosition, lastPosition);

    }
}
