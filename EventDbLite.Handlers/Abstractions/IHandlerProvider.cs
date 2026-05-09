namespace EventDbLite.Handlers.Abstractions;
public interface IHandlerProvider
{
    Handler? GetHandlerMethod(Type handlerType, string identifier);

    SnapshotHandler? GetSnapshotHandler(Type handlerType);
    Handler? GetRestoreHandler(Type handlerType, string identifier);
}