using EventDbLite.Abstractions;
using EventDbLite.Handlers;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Streams;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace EventDbLite.Projections;

internal class LiveProjectionManager : IAsyncDisposable
{
    private readonly LiveProjectionRequirement _requirement;
    private readonly IEventStoreLite _eventStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventSerializer _serializer;
    private readonly IAsyncHandlerProvider _asyncHandlerProvider;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task _continueTask = Task.CompletedTask;

    private class VersionWaiter
    {
        public Position GlobalPosition { get; }
        public TaskCompletionSource CompletionSource { get; }
        public VersionWaiter(Position globalPosition)
        {
            GlobalPosition = globalPosition;
            CompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private Position _currentGlobalPosition = Position.Start;

    private readonly object _waitingTasksLock = new();
    private List<VersionWaiter> _waitingTasks = new();
    public LiveProjectionManager(IServiceProvider serviceProvider, IEventSerializer serializer, IAsyncHandlerProvider asyncHandlerProvider, IEventStoreLite eventStore, LiveProjectionRequirement requirement)
    {
        _requirement = requirement ?? throw new ArgumentNullException(nameof(requirement));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _asyncHandlerProvider = asyncHandlerProvider ?? throw new ArgumentNullException(nameof(asyncHandlerProvider));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task Start(CancellationToken token)
    {
        using IServiceScope initialScope = _serviceProvider.CreateScope();
        IEventSerializer eventSerializer = initialScope.ServiceProvider.GetRequiredService<IEventSerializer>();

        IStreamSubscription subscription = _requirement.Stream is not null
            ? _eventStore.SubscribeToStream(_requirement.Stream, StreamPosition.Start)
            : _eventStore.SubscribeToAllStreams(Position.Start);

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

        _continueTask = Monitor(subscription, _cancellationTokenSource.Token);
    }

    private void NotifyWaitingTasks(Position globalPosition)
    {
        lock (_waitingTasksLock)
        {
            List<VersionWaiter> remaining = new();
            foreach (VersionWaiter waiter in _waitingTasks)
            {
                if (!waiter.GlobalPosition.IsAfter(globalPosition))
                {
                    waiter.CompletionSource.TrySetResult();
                }
                else
                {
                    remaining.Add(waiter);
                }
            }
            _waitingTasks = remaining;
        }
    }
    private async Task Monitor(IStreamSubscription subscription, CancellationToken token)
    {
        await foreach (SubscriptionMessage nextMessage in subscription.Messages(token))
        {
            if(nextMessage is not SubscriptionMessage.Event nextEvent)
            {
                continue;
            }
            await RaiseProjectionEventAsync(nextEvent.SubscriptionEvent);
            _currentGlobalPosition = nextEvent.SubscriptionEvent.GlobalOrdinal;
            NotifyWaitingTasks(_currentGlobalPosition);
        }
    }
    private async Task RaiseProjectionEventAsync(StreamEvent streamEvent)
    {
        EventMetadata? metadata = _serializer.DeserializeMetadata(streamEvent.Data.Metadata);
        if(metadata is null)
        {
            return;
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        object? projection = ActivatorUtilities.GetServiceOrCreateInstance(scope.ServiceProvider, _requirement.ProjectionType);
        AsyncHandler? handler = _asyncHandlerProvider.GetHandlerMethod(projection.GetType(), metadata.Identifier);

        if (handler is null)
        {
            return;
        }

        object? payload = _serializer.DeserializeEvent(streamEvent.Data.Payload, handler.TargetType)
            ?? throw new InvalidOperationException($"Failed to deserialize event payload for identifier '{metadata.Identifier}'");

        await handler.Action(projection, payload);
    }

    public async ValueTask Stop()
    {
        if (_cancellationTokenSource is not null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        await _continueTask;
    }

    public ValueTask DisposeAsync() => Stop();

}
