using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public record PulledProjection<T>(T Object, string? StreamName, Position StartPosition, StreamPosition StreamStartPosition, long AppliedEvents, long PassedEvents, StreamPosition FinalStreamPosition, Position FinalPosition);