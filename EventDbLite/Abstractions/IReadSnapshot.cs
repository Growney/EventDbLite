using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public interface IReadSnapshot
{
    public string Identifier { get; }
    public Position Position { get; }
    public StreamPosition StreamPosition { get; }
}
