using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Abstractions;

public interface ISnapshotRepository
{
    IAsyncEnumerable<IReadSnapshot> GetSnapshots(string snapshotKey);
    object? DeserializeSnapshot(IReadSnapshot snapshot, Type targetType);
    Task StoreSnapshot(string snapshotKey, object data, string identifier, Position position, StreamPosition streamPosition);
}
