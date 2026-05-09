using EventDbLite.Abstractions;

namespace EventDbLite.Reactions.Abstractions;
public interface IReactionProviderFactory
{
    public IAsyncEnumerable<ReactionEvent<TEvent>> CreateProvider<TEvent>(StreamPosition initialPosition, string streamName);
    public IAsyncEnumerable<ReactionEvent<TEvent>> CreateProvider<TEvent>(Position initialPosition);
}
