using EventDbLite.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Projections;

public class InMemorySnapshopRepository : ISnapshotRepository
{
    private class InMemorySnapshot : IReadSnapshot
    {
        public required object Data { get; set; }
        public required string Identifier { get; set; }
        public required Position Position { get; set; }
        public required StreamPosition StreamPosition{ get; set; }
    }

    private readonly ConcurrentDictionary<string, InMemorySnapshot> _snapshots = new();

    public IAsyncEnumerable<IReadSnapshot> GetSnapshots(string snapshotKey)
    {
        if (_snapshots.TryGetValue(snapshotKey, out InMemorySnapshot? snapshot))
        {
            return AsyncEnumerable.Repeat(snapshot, 1);
        }

        return AsyncEnumerable.Empty<IReadSnapshot>();
    }

    public async Task StoreSnapshot(string snapshotKey, object data, string identifier, Position position, StreamPosition streamPosition)
    {
        _snapshots.AddOrUpdate(snapshotKey, 
        (key) =>
        {
            return new InMemorySnapshot()
            {
                Data = data,
                Identifier = identifier,
                Position = position,
                StreamPosition = streamPosition
            };
        }, 
        (key, existing) =>
        {
            if (!(existing.Position.IsAfter(position) || existing.StreamPosition.IsAfter(streamPosition)))
            {

                existing.Position = position;
                existing.Data = data;
                existing.StreamPosition = streamPosition;
            }
            

            return existing;
        });
    }

    public object? DeserializeSnapshot(IReadSnapshot snapshot, Type targetType)
    {
        if(snapshot is not InMemorySnapshot inMemorySnapshot)
        {
            throw new NotSupportedException("Snapshot type not supported by repository");
        }

        if(inMemorySnapshot.Data.GetType() != targetType)
        {
            return null;
        }

        return inMemorySnapshot.Data;
    }
}
