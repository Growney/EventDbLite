namespace EventDbLite.Abstractions;

public interface IProjectionProvider
{
    Task<T> Load<T>(string streamName, StreamPosition until);
    Task<T> Load<T>(Position until);
}
