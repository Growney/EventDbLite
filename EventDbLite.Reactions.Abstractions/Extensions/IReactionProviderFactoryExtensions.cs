using EventDbLite.Abstractions;
using System.Runtime.CompilerServices;

namespace EventDbLite.Reactions.Abstractions;
public static class IReactionProviderFactoryExtensions
{
    public static Task On<TEvent>(this IReactionProviderFactory factory, Func<TEvent, Task> handler, CancellationToken token, string streamName)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(StreamPosition.End, streamName);
        return OnInternal(provider, handler, token);
    }

    public static Task On<TEvent>(this IReactionProviderFactory factory, Func<TEvent, Task> handler, StreamPosition initialPosition, CancellationToken token, string streamName)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(initialPosition, streamName);

        return OnInternal(provider, handler, token);
    }
    public static Task On<TEvent>(this IReactionProviderFactory factory, Func<TEvent, Task> handler, CancellationToken token)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(Position.End);
        return OnInternal(provider, handler, token);
    }

    public static Task On<TEvent>(this IReactionProviderFactory factory, Func<TEvent, Task> handler, Position initialPosition, CancellationToken token)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(initialPosition);

        return OnInternal(provider, handler, token);
    }
    private static async Task OnInternal<TEvent>(IAsyncEnumerable<ReactionEvent<TEvent>> provider, Func<TEvent, Task> handler, CancellationToken token)
    {
        await foreach (ReactionEvent<TEvent> reactionEvent in provider.WithCancellation(token))
        {
            await handler(reactionEvent.Payload);
        }
    }

    public static IAsyncEnumerable<TEvent> Stream<TEvent>(this IReactionProviderFactory factory, CancellationToken token, StreamPosition initialPosition, string streamName)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(initialPosition, streamName);
        return StreamInternal(provider, token);
    }

    public static IAsyncEnumerable<TEvent> Stream<TEvent>(this IReactionProviderFactory factory, CancellationToken token, string streamName)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(StreamPosition.End, streamName);
        return StreamInternal(provider, token);
    }

    public static IAsyncEnumerable<TEvent> Stream<TEvent>(this IReactionProviderFactory factory, CancellationToken token, Position initialPosition)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(initialPosition);
        return StreamInternal(provider, token);
    }

    public static IAsyncEnumerable<TEvent> Stream<TEvent>(this IReactionProviderFactory factory, CancellationToken token)
    {
        IAsyncEnumerable<ReactionEvent<TEvent>> provider = factory.CreateProvider<TEvent>(Position.End);
        return StreamInternal(provider, token);
    }

    private static async IAsyncEnumerable<TEvent> StreamInternal<TEvent>(IAsyncEnumerable<ReactionEvent<TEvent>> provider, [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (ReactionEvent<TEvent> reactionEvent in provider.WithCancellation(token))
        {
            yield return reactionEvent.Payload;
        }
    }
}
