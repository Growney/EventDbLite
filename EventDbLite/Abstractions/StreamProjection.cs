using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public record StreamProjection<T>(T Object, string StreamName, StreamPosition StartPosition, StreamPosition TargetPosition)
{
    public static StreamProjection<T> FromStart(T instance, string streamName) => new(instance, streamName, StreamPosition.Start, StreamPosition.End);
    public static StreamProjection<T> FromPosition(T instance, string streamName, StreamPosition start) => new(instance, streamName,start, StreamPosition.End);
    public static StreamProjection<T> Until(T instance, string streamName, StreamPosition until) => new(instance,streamName, StreamPosition.Start, until);
    public static StreamProjection<T> Between(T instance, string streamName,StreamPosition start, StreamPosition until) => new(instance,streamName, start, until);
};
