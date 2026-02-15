using EventDbLite.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Projections;

public static class IProjectionProviderExtensions
{
    public static Task<T> Load<T>(IProjectionProvider provider) => provider.Load<T>(null, StreamPosition.End);
    public static Task<T> Load<T>(IProjectionProvider provider, string streamName) => provider.Load<T>(streamName, StreamPosition.End);
    public static Task<T> Load<T>(IProjectionProvider provider, StreamPosition until) => provider.Load<T>(null, until);
}
