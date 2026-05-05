namespace EventDbLite.Abstractions;

public readonly struct StreamPosition
{
    public ulong Position { get; }

    public StreamPosition(ulong position)
    {
        Position = position;
    }

    public static StreamPosition Start => new(ulong.MinValue);
    public static StreamPosition End => new(ulong.MaxValue);
    public static StreamPosition At(ulong position) => new(position);

    public static implicit operator ulong(StreamPosition streamPosition) => streamPosition.Position;
    public static implicit operator StreamPosition(ulong position) => new(position);
    public static implicit operator StreamState(StreamPosition streamPosition) => new((long)streamPosition.Position);

    public readonly StreamPosition Next() => new(Position + 1);
    public readonly ulong Difference(StreamPosition other) => Position - other.Position;
    public readonly bool IsAfter(StreamPosition other) => Position > other.Position;

    public override string ToString() => Position.ToString();
}