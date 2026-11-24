using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.Games
{
        public sealed record PublishGameCommand(Guid GameId, Guid LockToken, string ActorId);
}
