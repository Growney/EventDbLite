using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace EventDbLite.Abstractions;

public readonly struct Position
{
    public ulong CommitPosition { get; }
    public ulong PreparePosition { get; }

    public Position(ulong commitPosition, ulong preparePosition)
    {
        CommitPosition = commitPosition;
        PreparePosition = preparePosition;
    }

    public static Position Start => new(ulong.MinValue, ulong.MinValue);
    public static Position End => new(ulong.MaxValue, ulong.MaxValue);

    public static bool operator ==(Position left, Position right) => left.CommitPosition == right.CommitPosition && left.PreparePosition == right.PreparePosition;
    public static bool operator !=(Position left, Position right) => !(left == right);

    public static Position At(ulong position) => new(position, position);
    public readonly Position Next() => new(CommitPosition + 1, PreparePosition + 1);
    public readonly ulong Difference(Position other) => CommitPosition - other.CommitPosition;
    public readonly bool IsAfter(Position other) => CommitPosition > other.CommitPosition;

    public override string ToString() => $"C:{CommitPosition} P:{PreparePosition}";
}