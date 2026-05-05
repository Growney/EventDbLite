using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public struct StreamState
{
    private static class Constants
    {
        public const long Start = 0;
        public const long End = long.MaxValue;
        public const long NoStream = -1;
        public const long Any = -2;
        public const long StreamExists = -3;

        public const long MinimumValidPosition = -3;
    }
    private readonly long _position;

    public StreamState(long position)
    {
        if (position < Constants.MinimumValidPosition)
        {
            throw new ArgumentOutOfRangeException(nameof(position), $"Invalid position");
        }

        _position = position;
    }
    public static StreamState NoStream => new(Constants.NoStream);
    public static StreamState Any => new(Constants.Any);
    public static StreamState StreamExists => new(Constants.StreamExists);
    public static StreamState Start => new(Constants.Start);
    public static StreamState End => new(Constants.End);
    public static StreamState At(long position) => new(position);

    public static implicit operator long(StreamState streamPosition) => streamPosition._position;
    public static implicit operator StreamState(long position) => new(position);
    public static implicit operator ulong(StreamState state) => (ulong)state._position;

    public override string ToString()
        => _position switch
        {
            Constants.NoStream => "NoStream",
            Constants.Any => "Any",
            Constants.StreamExists => "StreamExists",
            Constants.Start => "Start",
            Constants.End => "End",
            _ => _position.ToString(),
        };
}