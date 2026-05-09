using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public interface ISnapshotRepository
{
    IAsyncEnumerable<Snapshot> GetSnapshots(string snapshotKey);
    Task StoreSnapshot(string snapshotKey, Snapshot snapshot);
}
