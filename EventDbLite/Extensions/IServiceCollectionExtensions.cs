using EventDbLite;
using EventDbLite.Abstractions;
using EventDbLite.Aggregates;
using EventDbLite.Handlers;
using EventDbLite.Handlers.Abstractions;
using EventDbLite.Projections;
using EventDbLite.Reactions;
using EventDbLite.Reactions.Abstractions;
using EventDbLite.Serialization;
using EventDbLite.Streams;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    private const string _defaultstorageKey = "default";
    public static IServiceCollection AddEventDbLite(this IServiceCollection services)
    {
        services.AddHostedService<ConstantReactionService>();

        services.TryAddSingleton<IEventStoreLite, EventStoreLite>();
        services.TryAddSingleton<ILiveProjectionRepository, LiveProjectionRepository>();

        services.TryAddSingleton<IEventSerializer, JsonEventSerializer>();
        services.TryAddSingleton<IHandlerProvider, HandlerProvider>();
        services.TryAddSingleton<IAsyncHandlerProvider, AsyncHandlerProvider>();

        services.TryAddTransient<IAggregateRepository, AggregateRepository>();
        services.TryAddTransient<IProjectionProvider, ProjectionProvider>();

        services.TryAddTransient<IStreamEventWriter, StreamEventWriter>();
        services.TryAddTransient<IReactionProviderFactory, ReactionProviderFactory>();
        services.TryAddTransient<IReactionClassFactory, ReactionClassFactory>();
        services.TryAddKeyedTransient<IConstantReactionPositionStorage, EventDbLiteConstantReactionPositionStorage>(_defaultstorageKey);

        services.AddHostedService<LiveProjectionService>();
        return services;
    }
    public static IServiceCollection AddConstantReactionPositionStorage<T>(this IServiceCollection services)
        where T : class, IConstantReactionPositionStorage
        => AddConstantReactionPositionStorage<T>(services, _defaultstorageKey);
    public static IServiceCollection AddConstantReactionPositionStorage<T>(this IServiceCollection services, string storageKey)
        where T : class, IConstantReactionPositionStorage
    {
        services.AddKeyedTransient<IConstantReactionPositionStorage, T>(storageKey);
        return services;
    }

    private static IServiceCollection AddLiveProjection(this IServiceCollection services, Type projectionType, string? streamName = null)
    {
        services.AddSingleton(new LiveProjectionRequirement(streamName, projectionType));
        return services;
    }
    public static IServiceCollection AddSingletonLiveProjection<TService, TImplementation>(this IServiceCollection services, string? streamName = null)
        where TService : class
        where TImplementation : class, TService
    {
        services.TryAddSingleton<TService, TImplementation>();
        services.AddLiveProjection(typeof(TService), streamName);

        return services;
    }
    public static IServiceCollection AddSingletonLiveProjection<TImplementation>(this IServiceCollection services, string? streamName = null)
        where TImplementation : class
    {
        services.TryAddSingleton<TImplementation>();
        services.AddLiveProjection(typeof(TImplementation), streamName);
        return services;
    }
    public static IServiceCollection AddScopedLiveProjection<TImplementation>(this IServiceCollection services, string? streamName = null)
        where TImplementation : class
    {
        services.TryAddScoped<TImplementation>();
        services.AddLiveProjection(typeof(TImplementation), streamName);
        return services;
    }
    public static IServiceCollection AddScopedLiveProjection<TService, TImplementation>(this IServiceCollection services, string? streamName = null)
        where TService : class
        where TImplementation : class, TService
    {
        services.TryAddScoped<TService, TImplementation>();
        services.AddLiveProjection(typeof(TService), streamName);
        return services;
    }
    public static IServiceCollection AddTransientLiveProjection<TImplementation>(this IServiceCollection services, string? streamName = null)
         where TImplementation : class
    {
        services.TryAddTransient<TImplementation>();
        services.AddLiveProjection(typeof(TImplementation), streamName);
        return services;
    }
    public static IServiceCollection AddTransientLiveProjection<TService, TImplementation>(this IServiceCollection services, string? streamName = null)
        where TService : class
        where TImplementation : class, TService
    {
        services.TryAddTransient<TService, TImplementation>();
        services.AddLiveProjection(typeof(TService), streamName);
        return services;
    }

    public static IServiceCollection AddConstantReaction<T>(this IServiceCollection services, Func<IServiceProvider, T, Task> reaction, string storageKey, string? reactionKey = null)
    {
        services.AddSingleton(new ConstantReactionSource([new ConstantReaction((serviceProvider, obj) =>
        {
            if (obj is T t)
            {
                return reaction(serviceProvider, t);
            }
            return Task.CompletedTask;
        }, typeof(T))], storageKey, reactionKey));

        return services;
    }
    public static IServiceCollection AddConstantReactionClass<T>(this IServiceCollection services) => AddConstantReactionClass<T>(services, _defaultstorageKey);
    public static IServiceCollection AddConstantReactionClass<T>(this IServiceCollection services, string storageKey) => AddConstantReactionClass<T>(services, storageKey, typeof(T).Name);
    public static IServiceCollection AddConstantReactionClass<T>(this IServiceCollection services, string storageKey, string? reactionKey)
    {
        services.AddSingleton(serviceProvider =>
        {
            List<ConstantReaction> reactions = GetReactions<T>(serviceProvider);
            return new ConstantReactionSource(reactions, storageKey: storageKey, reactionKey: reactionKey);
        });
        return services;
    }
    public static IServiceCollection AddConstantReactionService<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
            => AddConstantReactionService<TService, TImplementation>(services, _defaultstorageKey);

    public static IServiceCollection AddConstantReactionService<TService, TImplementation>(this IServiceCollection services, string storageKey)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TService, TImplementation>();
        services.AddConstantReactionClass<TService>(storageKey);
        return services;
    }

    public static IServiceCollection AddConstantReactionService<TService, TImplementation>(this IServiceCollection services, string storageKey, string? reactionKey)
        where TService : class
        where TImplementation : class, TService
    {
        services.TryAddSingleton<TService, TImplementation>();
        services.AddConstantReactionClass<TService>(storageKey, reactionKey);
        return services;
    }

    private static List<ConstantReaction> GetReactions<T>(IServiceProvider serviceProvider)
    {
        IAsyncHandlerProvider handlerProvider = serviceProvider.GetRequiredService<IAsyncHandlerProvider>();
        IEnumerable<AsyncHandler> handlers = handlerProvider.GetHandlerMethods(typeof(T));

        List<ConstantReaction> reactions = [];

        foreach (AsyncHandler handler in handlers)
        {
            ConstantReaction reaction = new(async (reactionServiceProvider, eventObject) =>
            {
                object? instance = ActivatorUtilities.GetServiceOrCreateInstance(reactionServiceProvider, typeof(T));

                await handler.Action.Invoke(instance, eventObject);

            }, handler.TargetType);

            reactions.Add(reaction);
        }
        return reactions;
    }
}
