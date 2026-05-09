using EventDbLite.Abstractions;
using EventDbLite.Projections;
using EventDbLite.Reactions.Abstractions;
using Microsoft.Extensions.Logging;

namespace EventDbLite.Reactions;
public class ReactionProviderFactory : IReactionProviderFactory
{
    private readonly IEventStoreLite _eventStore;
    private readonly IEventSerializer _eventSerializer;
    private readonly ILoggerFactory _loggerProvider;

    public ReactionProviderFactory(IEventStoreLite eventStore, IEventSerializer eventSerializer,ILoggerFactory loggerProvider)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _loggerProvider = loggerProvider ?? throw new ArgumentNullException(nameof(loggerProvider));
    }

    public IAsyncEnumerable<ReactionEvent<TEvent>> CreateProvider<TEvent>(StreamPosition initialPosition, string streamName)
    {
        ILogger<ReactionProvider<TEvent>> logger = _loggerProvider.CreateLogger<ReactionProvider<TEvent>>();
        return new ReactionProvider<TEvent>(_eventSerializer, logger, _eventStore, streamName, initialPosition);
    }

    public IAsyncEnumerable<ReactionEvent<TEvent>> CreateProvider<TEvent>(Position initialPosition)
    {
        ILogger<ReactionProvider<TEvent>> logger = _loggerProvider.CreateLogger<ReactionProvider<TEvent>>();
        return new ReactionProvider<TEvent>(_eventSerializer, logger, _eventStore, initialPosition);
    }
}
