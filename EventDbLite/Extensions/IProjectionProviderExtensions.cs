using EventDbLite.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public static class IProjectionProviderExtensions
{
    public static Task<T> Load<T>(this IProjectionProvider provider) => provider.Load<T>(Position.End);
    public static Task<T> Load<T>(this IProjectionProvider provider, string streamName) => provider.Load<T>(streamName, StreamPosition.End);
}
