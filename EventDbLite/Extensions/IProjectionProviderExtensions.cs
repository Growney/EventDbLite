using EventDbLite.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EventDbLite.Abstractions;

public static class IProjectionProviderExtensions
{
    public static async Task<PulledProjection<TValue>> CloneOrPull<TValue>(this IProjectionProvider service, PulledProjection<TValue>? pulledProjection) where TValue : notnull
    {
        if (pulledProjection is null)
        {
            Projection<TValue> projection = await service.CloneAsync<TValue>();
            PulledProjection<TValue> pulled = await service.PullAsync<TValue>(projection, Position.End);
            return pulled;
        }
        else
        {
            return await service.PullAsync<TValue>(pulledProjection, Position.End);
        }
    }
    public static Task<TValue> ClonePullReadPushAsync<TValue, TProjection>(this IProjectionProvider service, Func<TProjection, TValue> selector) where TProjection : notnull
        => service.ClonePullReadPushAsync<TValue, TProjection>(selector, null);
    public static async Task<TValue> ClonePullReadPushAsync<TValue, TProjection>(this IProjectionProvider service, Func<TProjection, TValue> selector, string? streamName) where TProjection : notnull
    {
        Projection<TProjection> projection = await service.CloneAsync<TProjection>(streamName);

    public static Task<TValue> ClonePullReadPushAsync<TValue, TProjection>(this IProjectionProvider service, Func<TProjection, TValue> selector, string streamName) where TProjection : notnull
        => service.ClonePullReadPushAsync(selector, streamName, StreamPosition.End);
    public static async Task<TValue> ClonePullReadPushAsync<TValue, TProjection>(this IProjectionProvider service, Func<TProjection, TValue> selector, string streamName, StreamPosition until) where TProjection : notnull
    {
        StreamProjection<TProjection> projection = await service.CloneAsync<TProjection>(streamName, until);

        PulledStreamProjection<TProjection> pulledProjection = await service.PullAsync<TProjection>(projection);
        TValue result = selector(pulledProjection.Object);

        await service.PushAsync(pulledProjection);

        return result;
    }
    public static Task<TValue> ClonePullReadPushAsync<TValue, TProjection>(this IProjectionProvider service, Func<TProjection, TValue> selector) where TProjection : notnull
        => service.ClonePullReadPushAsync(selector, Position.End);
    public static async Task<TValue> ClonePullReadPushAsync<TValue, TProjection>(this IProjectionProvider service, Func<TProjection, TValue> selector, Position until) where TProjection : notnull
    {
        AllStreamProjection<TProjection> projection = await service.CloneAsync<TProjection>(until);

        PulledAllStreamProjection<TProjection> pulledProjection = await service.PullAsync<TProjection>(projection);
        TValue result = selector(pulledProjection.Object);

        await service.PushAsync(pulledProjection);

        return result;
    }

    public static Task<AllStreamProjection<TProjection>> CloneAsync<TProjection>(this IProjectionProvider service) where TProjection : notnull => service.CloneAsync<TProjection>(Position.End);
    public static Task<StreamProjection<TProjection>> CloneAsync<TProjection>(this IProjectionProvider service, string streamName) where TProjection : notnull => service.CloneAsync<TProjection>(streamName, StreamPosition.End);

    public static Task<T> LoadAsync<T>(this IProjectionProvider service) where T : notnull
     => service.LoadBetweenAsync<T>(Position.Start, Position.End);
    public static Task<T> LoadAsync<T>(this IProjectionProvider service, Position startPosition) where T : notnull
    => service.LoadBetweenAsync<T>(startPosition, Position.End);

    public static Task<T> LoadAsync<T>(this IProjectionProvider service, string streamName) where T : notnull
    => service.LoadBetweenAsync<T>(streamName, StreamPosition.Start, StreamPosition.End);
    public static Task<T> LoadAsync<T>(this IProjectionProvider service, string streamName, StreamPosition startPosition) where T : notnull
    => service.LoadBetweenAsync<T>(streamName, startPosition, StreamPosition.End);

    public static Task<T> LoadUntilAsync<T>(this IProjectionProvider service, Position endPosition) where T : notnull
    => service.LoadBetweenAsync<T>(Position.Start, endPosition);
    public static Task<T> LoadUntilAsync<T>(this IProjectionProvider service, string streamName, StreamPosition endPosition) where T : notnull
    => service.LoadBetweenAsync<T>(streamName, StreamPosition.Start, endPosition);

    public static async Task<T> LoadBetweenAsync<T>(this IProjectionProvider service, string streamName, StreamPosition startPosition, StreamPosition endPosition) where T : notnull
    {
        T instance = service.CreateInstance<T>();

        StreamProjection<T> projection = StreamProjection<T>.Between(instance, streamName, startPosition, endPosition);

        PulledStreamProjection<T> pulled = await service.PullAsync<T>(projection);

        return pulled.Object;
    }

    public static async Task<T> LoadBetweenAsync<T>(this IProjectionProvider service, Position startPosition, Position endPosition) where T : notnull
    {
        T instance = service.CreateInstance<T>();

        AllStreamProjection<T> projection = AllStreamProjection<T>.Between(instance, startPosition, endPosition);

        PulledAllStreamProjection<T> pulled = await service.PullAsync<T>(projection);

        return pulled.Object;
    }
}
