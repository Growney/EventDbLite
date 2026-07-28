using EventDbLite.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EventDbLite.MongoDb;

public class MongoDbSnapshotRepository : ISnapshotRepository
{
    private readonly IMongoClient _mongoClient;

    private class MongoDbSnapshot : IReadSnapshot
    {
        public required BsonDocument Document { get; set; }
        public required string Identifier { get; set; }
        public required Position Position { get; set; }
        public required StreamPosition StreamPosition { get; set; }
    }

    public MongoDbSnapshotRepository(IMongoClient mongoClient)
    {
        _mongoClient = mongoClient;
    }

    public object? DeserializeSnapshot(IReadSnapshot snapshot, Type targetType)
    {
        if(snapshot is not MongoDbSnapshot mongoSnapshot)
        {
            throw new NotSupportedException("Snapshot type not supported");
        }

        string json = mongoSnapshot.Document["data"].ToJson();

        return JsonSerializer.Deserialize(json, targetType);
    }

    public IAsyncEnumerable<IReadSnapshot> GetSnapshots(string snapshotKey)
    {
        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort
            .Descending("position.prepare-position")
            .Descending("position.commit-position")
            .Descending("stream-position");

        IMongoDatabase database = _mongoClient.GetDatabase("snapshot");
        IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(snapshotKey);

        return collection.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(sort)
            .ToAsyncEnumerable()
            .Select(x => new MongoDbSnapshot()
            {
                Document = x,
                Identifier = x["identifier"].ToString() ?? throw new InvalidOperationException("Identifier not found"),
                Position = new Position(ulong.Parse(x["position"]["commit-position"].ToJson()), ulong.Parse(x["position"]["prepare-position"].ToJson())),
                StreamPosition = new StreamPosition(ulong.Parse(x["stream-position"].ToJson()))

            });
    }

    public async Task StoreSnapshot(string snapshotKey, object data, string identifier, Position position, StreamPosition streamPosition)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(data, data.GetType());

        if(node is null)
        {
            return;
        }

        JsonObject positionObject = new()
        {
            ["prepare-position"] = position.PreparePosition,
            ["commit-position"] = position.CommitPosition,
        };

        JsonObject root = new ()
        {
            ["identifier"] = identifier,
            ["position"] = positionObject,
            ["stream-position"] = streamPosition.Position,
            ["data"] = node
        };

        string json = root.ToString();

        BsonDocument document = BsonDocument.Parse(json);

        IMongoDatabase database = _mongoClient.GetDatabase("snapshot");
        IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(snapshotKey);

        await collection.InsertOneAsync(document);
    }
}
