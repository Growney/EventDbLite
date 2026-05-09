using EventDbLite.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Projections;

public class InMemorySnapshopRepository : ISnapshotRepository
{
    private readonly ConcurrentDictionary<string, Snapshot> _snapshots = new();

    public IAsyncEnumerable<Snapshot> GetSnapshots(string snapshotKey)
    {
        if (_snapshots.TryGetValue(snapshotKey, out Snapshot? snapshot))
        {
            return AsyncEnumerable.Repeat(snapshot, 1);
        }

        return AsyncEnumerable.Empty<Snapshot>();
    }

    public async Task StoreSnapshot(string snapshotKey, Snapshot snapshot)
    {
        _snapshots.AddOrUpdate(snapshotKey, snapshot, (key, existing) =>
            {
                if (existing.Position.IsAfter(snapshot.Position))
                {
                    return existing;

                }
                else
                {
                    return snapshot;
                }
            }
        );
    }
}
