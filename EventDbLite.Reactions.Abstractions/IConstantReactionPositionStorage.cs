using EventDbLite.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Reactions.Abstractions;

public interface IConstantReactionPositionStorage
{
    public Task<Position?> GetPositionAsync(string reactionKey);
    public Task SetPositionAsync(string reactionKey, Position position);

}
