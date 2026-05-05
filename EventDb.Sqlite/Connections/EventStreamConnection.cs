using EventDb.Sqlite.Abstractions;
using EventDbLite.Abstractions;
using EventDbLite.Exceptions;
using EventDbLite.Streams;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EventDb.Sqlite.Connections;

internal class EventStreamConnection(ISqliteConnectionFactory connectionFactory) : IEventStreamConnection
{
    private readonly ISqliteConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    public async Task<IEnumerable<StreamEvent>> AppendToStreamAsync(string streamName, IEnumerable<EventData> data, StreamState expectedState)
    {
        await _semaphore.WaitAsync();
        try
        {
            SqliteConnection sqliteConnection = _connectionFactory.CreateConnection(SqliteOpenMode.ReadWrite);

            sqliteConnection.Open();

            SqliteTransaction transaction = sqliteConnection.BeginTransaction(deferred: true);

            StreamPosition? currentStreamVersion = null;

            using (SqliteCommand checkNoStreamCommand = sqliteConnection.CreateCommand())
            {
                checkNoStreamCommand.CommandText =
                    @"SELECT MAX(StreamOrdinal)
                    FROM PersistedEvents
                    WHERE StreamName = $streamName;";
                checkNoStreamCommand.Parameters.AddWithValue("$streamName", streamName);

                object? result = checkNoStreamCommand.ExecuteScalar();

                if(result != null && result != DBNull.Value)
                {
                    currentStreamVersion = new((ulong)(long)result);
                } 

                if (expectedState == StreamState.NoStream)
                {
                    if(currentStreamVersion.HasValue)
                        throw new ConcurrencyException(StreamState.NoStream, currentStreamVersion.Value);
                }
                else if (expectedState == StreamState.StreamExists)
                {
                    if(currentStreamVersion == 0)
                        throw new ConcurrencyException(StreamState.StreamExists, currentStreamVersion.Value);
                }
                else if (expectedState != StreamState.Any)
                {
                    if(expectedState != currentStreamVersion)
                        throw new ConcurrencyException(expectedState, currentStreamVersion ?? StreamPosition.Start);
                }
            }
            

            using (SqliteCommand writeCommand = sqliteConnection.CreateCommand())
            {
                writeCommand.CommandText =
                    @"INSERT INTO PersistedEvents (Id, StreamName, StreamOrdinal, Payload, Metadata, Identifier)
                    VALUES ($id, $streamName, $streamOrdinal, $payload, $metadata, $identifier);
                    SELECT last_insert_rowid();";
                SqliteParameter idParam = writeCommand.Parameters.Add("$id", SqliteType.Text);
                SqliteParameter streamNameParam = writeCommand.Parameters.Add("$streamName", SqliteType.Text);
                SqliteParameter streamOrdinalParam = writeCommand.Parameters.Add("$streamOrdinal", SqliteType.Integer);
                SqliteParameter payloadParam = writeCommand.Parameters.Add("$payload", SqliteType.Blob);
                SqliteParameter metadataParam = writeCommand.Parameters.Add("$metadata", SqliteType.Blob);
                SqliteParameter identifierParam = writeCommand.Parameters.Add("$identifier", SqliteType.Text);
                List<StreamEvent> createdEvents = new();
                foreach (var eventData in data)
                {
                    //If we are not at the start of the stream we want to increment the stream version before.
                    currentStreamVersion = currentStreamVersion?.Next() ?? StreamPosition.Start;

                    idParam.Value = Guid.NewGuid().ToString();
                    streamNameParam.Value = streamName;
                    streamOrdinalParam.Value = currentStreamVersion.Value.Position;
                    payloadParam.Value = eventData.Payload;
                    metadataParam.Value = eventData.Metadata;
                    identifierParam.Value = eventData.Identifier;
                    long globalId = 0;
                    using (var reader = writeCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            globalId = reader.GetInt64(0);
                        }
                    }
                    createdEvents.Add(new StreamEvent(
                        Guid.Parse(idParam.Value.ToString()!),
                        streamName,
                        currentStreamVersion.Value,
                        new Position((ulong)globalId, (ulong)globalId),
                        new EventData((byte[])payloadParam.Value, (byte[])metadataParam.Value, identifierParam.Value.ToString()!)
                    ));
                    
                }
                transaction.Commit();
                return createdEvents;
            }
        }
        finally
        {
            _semaphore.Release();
        }

    }
    public async Task<StreamEvent> AppendToStreamAsync(string streamName, EventData data, StreamState expectedState) => (await AppendToStreamAsync(streamName, Enumerable.Repeat(data, 1), expectedState)).First();
    public async IAsyncEnumerable<StreamEvent> ReadAllStreamEvents(StreamDirection direction, Position position)
    {
        //This feels wrong, using it to allow the use of IasyncEnumerable on a clearly synchronous method
        //Lets see how it get on
        await Task.CompletedTask;

        SqliteConnection sqliteConnection = _connectionFactory.CreateConnection(SqliteOpenMode.ReadOnly);

        sqliteConnection.Open();

        SqliteCommand command = sqliteConnection.CreateCommand();

        try
        {
            command.CommandText =
            @"SELECT Id, StreamName, StreamOrdinal, GlobalOrdinal, Payload, Metadata, Identifier
              FROM PersistedEvents
              WHERE (( $direction = 0 AND GlobalOrdinal > $position ) OR ( $direction = 1 AND GlobalOrdinal < $position ))
              ORDER BY GlobalOrdinal " + (direction == StreamDirection.Forward ? "ASC" : "DESC") + ";";

            command.Parameters.AddWithValue("$direction", direction == StreamDirection.Forward ? 0 : 1);
            //We must truncate the max value to long max else SQLLite wont understand it
            command.Parameters.AddWithValue("$position", Math.Min(position.CommitPosition, long.MaxValue));


            SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                yield return new StreamEvent(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    new StreamPosition((ulong)reader.GetInt64(2)),
                    new Position((ulong)reader.GetInt64(3), (ulong)reader.GetInt64(3)),
                    new EventData(
                        (byte[])reader["Payload"],
                        (byte[])reader["Metadata"],
                        reader.GetString(6)
                    )
                );
            }
        }
        finally
        {
            command.Dispose();
            sqliteConnection.Close();
            sqliteConnection.Dispose();
        }

    }

    public async IAsyncEnumerable<StreamEvent> ReadStreamEvents(string streamName, StreamDirection direction, StreamPosition position)
    {
        //This feels wrong, using it to allow the use of IasyncEnumerable on a clearly synchronous method
        //Lets see how it get on
        await Task.CompletedTask;

        SqliteConnection sqliteConnection = _connectionFactory.CreateConnection(SqliteOpenMode.ReadOnly);

        sqliteConnection.Open();

        SqliteCommand command = sqliteConnection.CreateCommand();

        try
        {
            command.CommandText =
            @"SELECT Id, StreamName, StreamOrdinal, GlobalOrdinal, Payload, Metadata, Identifier
              FROM PersistedEvents
              WHERE StreamName = $streamName
              AND (( $direction = 0 AND StreamOrdinal >= $position ) OR ( $direction = 1 AND StreamOrdinal <= $position ))
              ORDER BY StreamOrdinal " + (direction == StreamDirection.Forward ? "ASC" : "DESC") + ";";

            command.Parameters.AddWithValue("$streamName", streamName);
            command.Parameters.AddWithValue("$direction", direction == StreamDirection.Forward ? 0 : 1);
            //We must truncate the max value to long max else SQLLite wont understand it
            command.Parameters.AddWithValue("$position", Math.Min(position.Position, long.MaxValue));

            SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                yield return new StreamEvent(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    new StreamPosition((ulong)reader.GetInt64(2)),
                    new Position((ulong)reader.GetInt64(3), (ulong)reader.GetInt64(3)),
                    new EventData(
                        (byte[])reader["Payload"],
                        (byte[])reader["Metadata"],
                        reader.GetString(6)
                    )
                );
            }
        }
        finally
        {
            command.Dispose();
            sqliteConnection.Close();
            sqliteConnection.Dispose();
        }
    }
}
