using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public record AllStreamProjection<T>(T Object,Position StartPosition, Position TargetPosition)
{
    public static AllStreamProjection<T> FromStart(T instance) => new(instance, Position.Start, Position.End);
    public static AllStreamProjection<T> FromPosition(T instance, Position start) => new(instance, start, Position.End);
    public static AllStreamProjection<T> Until(T instance, Position until) => new(instance,Position.Start, until);
    public static AllStreamProjection<T> Between(T instance,Position start, Position until) => new(instance, start, until);
};
