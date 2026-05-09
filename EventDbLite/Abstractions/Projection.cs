using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public record Projection<T>(T Object, string? StreamName, Position StartPosition, StreamPosition StreamStartPosition)
{
    public static Projection<T> FromStart(T instance) => new(instance, null, Position.Start, StreamPosition.Start);
    public static Projection<T> FromStart(T instance, string? streamName) => new(instance, streamName, Position.Start, StreamPosition.Start);
    public static Projection<T> FromPosition(T instance, Position start) => new(instance, null, start, StreamPosition.Start);
    public static Projection<T> FromStreamPosition(T instance, string streamName, StreamPosition start) => new(instance, streamName, Position.Start, start);
};
