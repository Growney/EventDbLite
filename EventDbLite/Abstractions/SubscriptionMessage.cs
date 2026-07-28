using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace EventDbLite.Abstractions;

public abstract record SubscriptionMessage
{
    public record Event(StreamEvent SubscriptionEvent) : SubscriptionMessage();

    public record NotFound : SubscriptionMessage;
    public record Ok : SubscriptionMessage;

    public record FirstStreamPosition(StreamPosition StreamPosition) : SubscriptionMessage();

    public record LastStreamPosition(StreamPosition StreamPosition) : SubscriptionMessage();

    public record LastAllStreamPosition(Position Position) : SubscriptionMessage();

    public record SubscriptionConfirmation(string SubscriptionId) : SubscriptionMessage();

    public record AllStreamCheckpointReached(Position Position) : SubscriptionMessage();

    public record StreamCheckpointReached(StreamPosition StreamPosition) : SubscriptionMessage();

    public record CaughtUp : SubscriptionMessage;

    public record FellBehind : SubscriptionMessage;

    public record Unknown : SubscriptionMessage;
}