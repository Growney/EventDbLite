using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public record PulledStreamProjection<T>(T Object, string StreamName, StreamPosition StartPosition, StreamPosition TargetPosition, long AppliedEvents, long PassedEvents, StreamPosition FinalPosition);