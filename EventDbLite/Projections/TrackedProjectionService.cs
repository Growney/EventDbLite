using EventDbLite.Abstractions;
using EventDbLite.Handlers;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Reactions;
using EventDbLite.Reactions.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        IEventStoreLite store = scope.ServiceProvider.GetRequiredService<IEventStoreLite>();

        Position startPosition = (await projectionProvider.CloneAsync<T>()).StartPosition;
        IStreamSubscription subscription = store.SubscribeToAllStreams(startPosition);

        bool isCaughtUp = false;
        long passedEvents = 0;
        Queue<(Position eventPosition, Handler handler, object eventObj)> applyQueue = new();
        await foreach (SubscriptionMessage subscriptionMessage in subscription.Messages(stoppingToken))
        {
            try
            {
                switch (subscriptionMessage)
                {
                    case SubscriptionMessage.Event eventMessage:
                        {
                            passedEvents++;
                            if (!TryGetEventHandler(eventMessage.SubscriptionEvent, out var handler))
                            {
                                continue;
                            }

                            object? payload = _eventSerializer.DeserializeEvent(eventMessage.SubscriptionEvent.Data.Payload, handler.TargetType);

                            if(payload == null)
                            {
                                continue;
                            }

                            applyQueue.Enqueue((eventMessage.SubscriptionEvent.GlobalOrdinal, handler, payload));
                        }
                        break;
                    case SubscriptionMessage.CaughtUp:
                        {
                            isCaughtUp = true;
                        }
                        break;
                    case SubscriptionMessage.FellBehind:
                        {
                            isCaughtUp = false;
                        }
                        break;
                }

                if (isCaughtUp && applyQueue.Any())
                {
                    var cloned = await projectionProvider.CloneAsync<T>();

                    long appliedEvents = 0;
                    Position? currentPosition = null; 
                    while (applyQueue.TryDequeue(out var eventToBeApplied))
                    {
                        currentPosition = eventToBeApplied.eventPosition;
                        if (!eventToBeApplied.eventPosition.IsAfter(cloned.StartPosition))
                        {
                            continue;
                        }

                        eventToBeApplied.handler.Action(cloned.Object, eventToBeApplied.eventObj);
                        appliedEvents++;
                    }

                    if (appliedEvents == 0 || currentPosition is null)
                    {
                        continue;
                    }
                    PulledAllStreamProjection<T> pulledProjection = new(cloned.Object, cloned.StartPosition, cloned.TargetPosition, appliedEvents, passedEvents, currentPosition.Value);

                    await projectionProvider.PushAsync(pulledProjection);
                    appliedEvents = 0;
                    passedEvents = 0;
                }

            }
            catch (Exception ex)
            {

            }
        }
    }
    private bool TryGetEventHandler(StreamEvent streamEvent,[NotNullWhen(true)] out Handler? handler)
    {
        handler = null;
        if (streamEvent.Data.Metadata.Length == 0)
        {
            return false;
        }

        EventMetadata? metadata = _eventSerializer.DeserializeMetadata(streamEvent.Data.Metadata);

        if (metadata is null)
        {
            return false;
        }

        handler = _handlerProvider.GetHandlerMethod(typeof(T), metadata.Identifier);
        if (handler is null)
        {
            return false;
        }
        return true;
    }
    private static async Task PushProjection(IProjectionProvider projectionProvider, AllStreamProjection<T> cloned, long appliedEvents, long passedEvents, Position position)
    {
        
    }
}
