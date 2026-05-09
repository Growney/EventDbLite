
namespace EventDbLite.Abstractions;

public record Snapshot(byte[] Data, string Identifier, Position Position, StreamPosition StreamPosition);
