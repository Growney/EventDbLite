using EventDbLite.Abstractions;
using EventDbLite.Reactions.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EventDbLite.Reactions;
public class ReactionClassFactory : IReactionClassFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ReactionClassFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IReactionClassContainer<T> Create<T>()
        where T : class
    {
        T instance = ActivatorUtilities.CreateInstance<T>(_serviceProvider);

        if (instance == null)
        {
            throw new InvalidOperationException();
        }

        return ActivatorUtilities.CreateInstance<ReactionClassContainer<T>>(_serviceProvider, instance);
    }
}
