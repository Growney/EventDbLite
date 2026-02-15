using EventDbLite.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Aggregates;

public static class AggregateStreamNameProvider
{
    public static string GetStreamName<AggregateType>(Guid aggregateKey) where AggregateType : AggregateRoot<Guid> => GetStreamName<AggregateType, Guid>(aggregateKey);
    public static string GetStreamName<AggregateType>(string aggregateKey) where AggregateType : AggregateRoot<string> => GetStreamName<AggregateType, string>(aggregateKey);
    public static string GetStreamName<AggregateType, KeyType>(KeyType aggregateKey) where AggregateType : AggregateRoot<KeyType> => $"{typeof(AggregateType).Name}-{aggregateKey}";
}
