namespace EventDbLite.Reactions;
public class ReactionHandled
{
    public ulong CommitPosition { get; set; }
    public ulong PreparePosition { get; set; }
}