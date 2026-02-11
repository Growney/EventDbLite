using System;
using System.Collections.Generic;
using System.Text;

namespace EventDbLite.Reactions;

public record StoredReactionRequirement(string reactionKey, Type projectionType);

