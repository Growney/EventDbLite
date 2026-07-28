using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public record PulledAllStreamProjection<T>(T Object, Position StartPosition, Position TargetPosition, long AppliedEvents, long PassedEvents, Position FinalPosition);