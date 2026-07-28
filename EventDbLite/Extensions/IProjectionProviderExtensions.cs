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

        PulledProjection<TProjection> pulledProjection = await service.PullAsync<TProjection>(projection, Position.End);
        TValue result = selector(pulledProjection.Object);

        await service.PushAsync(pulledProjection);

        return result;
    }

    public static Task<T> LoadAsync<T>(this IProjectionProvider service) where T : notnull
        => service.LoadAsync<T>(Position.Start);

    public static async Task<T> LoadAsync<T>(this IProjectionProvider service, Position startPosition) where T : notnull
    {
        T instance = service.CreateInstance<T>();

        Projection<T> projection = Projection<T>.FromPosition(instance, startPosition);

        PulledProjection<T> pulled = await service.PullAsync<T>(projection, Position.End);

        return pulled.Object;
    }
    public static Task<T> LoadAsync<T>(this IProjectionProvider service, string streamName) where T : notnull
        => service.LoadAsync<T>(streamName, StreamPosition.Start);

    public static async Task<T> LoadAsync<T>(this IProjectionProvider service, string streamName, StreamPosition startPosition) where T : notnull
    {
        T instance = service.CreateInstance<T>();

        Projection<T> projection = Projection<T>.FromStreamPosition(instance, streamName, startPosition);

        PulledProjection<T> pulled = await service.PullAsync<T>(projection, Position.End);
        return pulled.Object;
    }
    public static Task<T> LoadUntilAsync<T>(this IProjectionProvider service, Position globalEndPosition) where T : notnull
        => service.LoadUntilAsync<T>(Position.Start, globalEndPosition);
    public static async Task<T> LoadUntilAsync<T>(this IProjectionProvider service, Position startPosition, Position globalEndPosition) where T : notnull
    {
        T instance = service.CreateInstance<T>();

        Projection<T> projection = Projection<T>.FromPosition(instance, startPosition);

        PulledProjection<T> pulled = await service.PullAsync<T>(projection, globalEndPosition);

        return pulled.Object;
    }
        public static Task<T> LoadUntilAsync<T>(this IProjectionProvider service, string streamName, Position globalEndPosition) where T : notnull
        => service.LoadUntilAsync<T>(streamName, StreamPosition.Start, globalEndPosition);
    public static async Task<T> LoadUntilAsync<T>(this IProjectionProvider service, string streamName, StreamPosition startPosition, Position globalEndPosition) where T : notnull
    {
        T instance = service.CreateInstance<T>();

        Projection<T> projection = Projection<T>.FromStreamPosition(instance, streamName, startPosition);

        PulledProjection<T> pulled = await service.PullAsync<T>(projection, globalEndPosition);

        return pulled.Object;
    }
}
