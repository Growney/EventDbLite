using EventDbLite.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Reactions.Abstractions;

public interface IConstantReactionPositionStorage
{
    public Task<StreamPosition?> GetPositionAsync(string reactionKey);
    public Task SetPositionAsync(string reactionKey, long globalPosition);

}
