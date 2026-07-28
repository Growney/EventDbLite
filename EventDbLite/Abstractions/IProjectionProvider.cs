namespace EventDbLite.Abstractions;

public interface IProjectionProvider
{
    T CreateInstance<T>();

    Task<StreamProjection<T>> CloneAsync<T>(string streamName, StreamPosition until) where T : notnull;
    Task<AllStreamProjection<T>> CloneAsync<T>(Position until) where T : notnull;

    Task<PulledStreamProjection<T>> PullAsync<T>(StreamProjection<T> projection) where T : notnull;
    Task<PulledAllStreamProjection<T>> PullAsync<T>(AllStreamProjection<T> projection) where T : notnull;

    Task PushAsync<T>(PulledStreamProjection<T> projection) where T : notnull;
    Task PushAsync<T>(PulledAllStreamProjection<T> projection) where T : notnull;
}
