namespace EventDbLite.Handlers;

public class SnapshotHandler(Func<object, object?> action)
{
    public Func<object, object?> Action { get; } = action ?? throw new ArgumentNullException(nameof(action));
}
