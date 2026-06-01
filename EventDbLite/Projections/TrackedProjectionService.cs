using EventDbLite.Abstractions;
using EventDbLite.Handlers;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Reactions;
using EventDbLite.Reactions.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static EventDbLite.Abstractions.SubscriptionMessage;

namespace EventDbLite.Projections;

public class TrackedProjectionService<T> : BackgroundService
    where T : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHandlerProvider _handlerProvider;
    private readonly IEventSerializer _eventSerializer;

    public TrackedProjectionService(IServiceProvider serviceProvider, IHandlerProvider handlerProvider, IEventSerializer eventSerializer)
    {
        _serviceProvider = serviceProvider;
        _handlerProvider = handlerProvider;
        _eventSerializer = eventSerializer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        IProjectionProvider projectionProvider = scope.ServiceProvider.GetRequiredService<IProjectionProvider>();

        AllStreamProjection<T> cloned = await projectionProvider.CloneAsync<T>();

        IEventStoreLite store = scope.ServiceProvider.GetRequiredService<IEventStoreLite>();

        IStreamSubscription subscription = store.SubscribeToAllStreams(cloned.StartPosition);

        bool isCaughtUp = false;
        long appliedEvents = 0;
        long passedEvents = 0;
        bool shouldSnapshot = false;
        Position? currentPosition = null;
        await foreach (SubscriptionMessage subscriptionMessage in subscription.Messages(stoppingToken))
        {
            try
            {
                switch (subscriptionMessage)
                {
                    case SubscriptionMessage.Event eventMessage:
                        {
                            currentPosition = eventMessage.SubscriptionEvent.GlobalOrdinal;
                            passedEvents++;
                            if (HandleEvent(cloned, eventMessage.SubscriptionEvent))
                            {
                                appliedEvents++;
                                shouldSnapshot = isCaughtUp;
                            }
                        }
                        break;
                    case SubscriptionMessage.CaughtUp:
                        {
                            shouldSnapshot = appliedEvents > 0;
                            isCaughtUp = true;
                        }
                        break;
                    case SubscriptionMessage.FellBehind:
                        {
                            isCaughtUp = false;
                        }
                        break;
                }

                if (shouldSnapshot && currentPosition.HasValue)
                {
                    await PushProjection(projectionProvider, cloned, appliedEvents, passedEvents, currentPosition.Value);
                    appliedEvents = 0;
                    passedEvents = 0;
                    shouldSnapshot = false;

                }

            }
            catch (Exception ex)
            {

            }
        }
    }
    private bool HandleEvent(AllStreamProjection<T> cloned, StreamEvent streamEvent)
    {
        if (streamEvent.Data.Metadata.Length == 0)
        {
            return false;
        }

        EventMetadata? metadata = _eventSerializer.DeserializeMetadata(streamEvent.Data.Metadata);

        if (metadata is null)
        {
            return false;
        }

        Handler? handler = _handlerProvider.GetHandlerMethod(cloned.Object.GetType(), metadata.Identifier);
        if (handler is null)
        {
            return false;
        }

        object? payload = _eventSerializer.DeserializeEvent(streamEvent.Data.Payload, handler.TargetType)
            ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{metadata.Identifier}'");

        handler.Action(cloned.Object, payload);
        return true;
    }
    private static async Task PushProjection(IProjectionProvider projectionProvider, AllStreamProjection<T> cloned, long appliedEvents, long passedEvents, Position position)
    {
        PulledAllStreamProjection<T> pulledProjection = new(cloned.Object, cloned.StartPosition, cloned.TargetPosition, appliedEvents, passedEvents, position);

        await projectionProvider.PushAsync(pulledProjection);
    }
}
