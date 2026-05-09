using EventDbLite.Abstractions;
using EventDbLite.Handlers.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EventDbLite.Handlers;

public class HandlerProvider(IEventSerializer eventSerializer) : IHandlerProvider
{
    private const string HandlerMethodName = "When";
    private const string SnapshotMethodName = "Snapshot";
    private const string RestoreMethodName = "Restore";

    private readonly ConcurrentDictionary<Type, Dictionary<string, Handler>> _handlerMethods = new();

    private readonly ConcurrentDictionary<Type, Dictionary<string, Handler>> _restoreMethods = new();
    private readonly ConcurrentDictionary<Type, SnapshotHandler?> _snapshotMethods = new();

    private readonly IEventSerializer _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));

    private Dictionary<string, Handler> RegisterHandler(string methodName,Type aggregateRootType)
    {
        Dictionary<string, Handler> handlerMethods = [];
        foreach (MethodInfo method in aggregateRootType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.Name != methodName)
            {
                continue;
            }

            if (method.ReturnType != typeof(void))
            {
                continue;
            }

            string? identifier = null;
            ParameterInfo[] methodParameters = method.GetParameters();
            if (methodParameters.Length != 1)
            {
                continue; // Skip methods that do not have exactly one parameter
            }

            ParameterInfo eventParameter = methodParameters[0];

            Type eventType = eventParameter.ParameterType;

            identifier = _eventSerializer.GetIdentifier(eventType);

            if (identifier is null)
            {
                continue; // Skip invalid methods
            }

            if (handlerMethods.ContainsKey(identifier))
            {
                throw new InvalidOperationException($"Duplicate handler method found: {identifier} in {aggregateRootType.FullName}");
            }

            Handler handler = new((instance, handleObj) =>
            {
                MethodInfo capturedMethod = method;
                capturedMethod.Invoke(instance, new[] { handleObj });
            }, eventType);

            handlerMethods.Add(identifier, handler);
        }

        return handlerMethods;
    }
    private SnapshotHandler? GetSnapshotMethod(string methodName, Type aggregateRootType)
    {
        foreach (MethodInfo method in aggregateRootType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.Name != methodName)
            {
                continue;
            }

            if (method.ReturnType == typeof(void))
            {
                continue;
            }

            ParameterInfo[] methodParameters = method.GetParameters();
            if (methodParameters.Length != 0)
            {
                continue; // Skip methods that do not have exactly one parameter
            }

            SnapshotHandler handler = new((instance) =>
            {
                MethodInfo capturedMethod = method;
                return capturedMethod.Invoke(instance, Array.Empty<object>());
            });

            return handler;
        }

        return null;
    }
    public Handler? GetHandlerMethod(Type handlerType, string identifier)
    {
        Dictionary<string, Handler> handlerMethods = _handlerMethods.GetOrAdd(handlerType, _ => RegisterHandler(HandlerMethodName, handlerType));

        if (!handlerMethods.TryGetValue(identifier, out Handler? method))
        {
            return null;
        }

        return method;
    }

    public SnapshotHandler? GetSnapshotHandler(Type handlerType)
    {
        SnapshotHandler? snapshotMethods = _snapshotMethods.GetOrAdd(handlerType, _ => GetSnapshotMethod(SnapshotMethodName, handlerType));
        
        return _snapshotMethods.GetOrAdd(handlerType, _ => GetSnapshotMethod(SnapshotMethodName, handlerType));
    }

    public Handler? GetRestoreHandler(Type handlerType, string identifier)
    {
        Dictionary<string, Handler> restoreHandlers = _restoreMethods.GetOrAdd(handlerType, _ => RegisterHandler(RestoreMethodName, handlerType));
        if (!restoreHandlers.TryGetValue(identifier, out Handler? method))
        {
            return null;
        }
        return method;
    }
}