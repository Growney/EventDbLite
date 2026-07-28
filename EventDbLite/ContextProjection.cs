using EventDbLite.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite;

public abstract class ContextProjection
{
    public EventMetadata? Metadata { get; set; }
}
