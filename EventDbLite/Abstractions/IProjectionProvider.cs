namespace EventDbLite.Abstractions;

public interface IProjectionProvider
{
    T CreateInstance<T>();
    Task<Projection<T>> CloneAsync<T>(string? streamName = null) where T : notnull;
    Task<PulledProjection<T>> PullAsync<T>(Projection<T> projection, Position until) where T : notnull;
    Task PushAsync<T>(PulledProjection<T> projection) where T : notnull;
}
