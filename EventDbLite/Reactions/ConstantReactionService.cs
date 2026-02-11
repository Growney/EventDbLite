using EventDbLite.Abstractions;
using EventDbLite.Reactions.Abstractions;
using EventDbLite.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventDbLite.Reactions;
public class ConstantReactionService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task _completionTask = Task.CompletedTask;

    public ConstantReactionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IEnumerable<ConstantReactionSource> reactionSources = _serviceProvider.GetServices<ConstantReactionSource>();

        Dictionary<(string reactionKey, string storageKey), List<ConstantReaction>> reactionMap = new();

        foreach (ConstantReactionSource reactionSource in reactionSources)
        {
            string reactionKey = reactionSource.ReactionKey ?? "default-reactions";
            string storageKey = reactionSource.StorageKey;

            if (!reactionMap.TryGetValue((reactionKey, storageKey), out var reactions))
            {
                reactions = new List<ConstantReaction>();
                reactionMap.Add((reactionKey, storageKey), reactions);
            }

            foreach (ConstantReaction reaction in reactionSource.Reactions)
            {
                reactions.Add(reaction);
            }
        }

        List<Task> reactionTasks = new();
        foreach (var kvp in reactionMap)
        {
            reactionTasks.Add(StreamReactions(kvp.Key.storageKey, kvp.Key.reactionKey, kvp.Value, _cancellationTokenSource.Token));
        }

        _completionTask = Task.WhenAll(reactionTasks);

        return Task.CompletedTask;
    }

    private async Task StreamReactions(string storageKey, string reactionKey, IEnumerable<ConstantReaction> handlers, CancellationToken token)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();

        IEventStoreLite store = scope.ServiceProvider.GetRequiredService<IEventStoreLite>();

        IConstantReactionPositionStorage positionStorage = scope.ServiceProvider.GetRequiredKeyedService<IConstantReactionPositionStorage>(storageKey);
        StreamPosition position = await positionStorage.GetPositionAsync(reactionKey) ?? StreamPosition.Beginning;

        IStreamSubscription subscription = store.SubscribeToAllStreams(position);

        IEventSerializer serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();
        Dictionary<string, Dictionary<Type, List<ConstantReaction>>> identifiedReactions = GroupReactions(handlers, serializer);
        await foreach (SubscriptionEvent streamEvent in subscription.StreamEvents(token))
        {
            try
            {
                if(streamEvent.Event.Data.Metadata.Length == 0)
                {
                    continue;
                }

                EventMetadata? metadata = serializer.DeserializeMetadata(streamEvent.Event.Data.Metadata);

                if(metadata is null)
                {
                    continue;
                }

                if (!identifiedReactions.TryGetValue(metadata.Identifier, out var eventHandlers))
                {
                    continue;
                }

                bool handledAny = false;
                foreach (var kvp in eventHandlers)
                {
                    object? eventObject = serializer.DeserializeEvent(streamEvent.Event.Data.Payload, kvp.Key);

                    if (eventObject is null)
                    {
                        continue;
                    }

                    using IServiceScope eventScope = scope.ServiceProvider.CreateScope();

                    foreach (ConstantReaction handler in kvp.Value)
                    {
                        try
                        {
                            await handler.Handler(eventScope.ServiceProvider, eventObject);
                            handledAny = true;
                        }
                        catch
                        {
                            //TODO do something with the exception
                        }
                    }
                }
                if (handledAny)
                {
                    await positionStorage.SetPositionAsync(reactionKey, streamEvent.Event.GlobalOrdinal);
                }
            }
            catch(Exception ex)
            {

            }
        }
    }

    private static Dictionary<string, Dictionary<Type, List<ConstantReaction>>> GroupReactions(IEnumerable<ConstantReaction> handlers, IEventSerializer serializer)
    {
        Dictionary<string, Dictionary<Type, List<ConstantReaction>>> identifiedReactions = new();

        foreach (ConstantReaction handler in handlers)
        {
            string identifier = serializer.GetIdentifier(handler.TargetType);

            if (!identifiedReactions.TryGetValue(identifier, out var typeReactions))
            {
                typeReactions = new Dictionary<Type, List<ConstantReaction>>();
                identifiedReactions.Add(identifier, typeReactions);
            }

            if (!typeReactions.TryGetValue(handler.TargetType, out var identifierReactions))
            {
                identifierReactions = new List<ConstantReaction>();
                typeReactions.Add(handler.TargetType, identifierReactions);
            }

            identifierReactions.Add(handler);
        }

        return identifiedReactions;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        await _completionTask;
    }
}
